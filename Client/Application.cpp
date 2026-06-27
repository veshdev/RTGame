#include "Application.h"

#include "GameSession.h"
#include "Interpolation.h"
#include "LocalPrediction.h"
#include "SceneManager.h"
#include "InputHandler.h"
#include "Protocol.h"
#include "WorldRenderer.h"
#include "UiSystem.h"
#include "AccountStorage.h"

#include <chrono>
#include <optional>

namespace {

void ApplySavedAccount(std::string& username, std::string& password, const SavedAccount& account) {
    username = account.username;
    password = account.password;
}

bool SelectedRoomNeedsPassword(const GameSession& session) {
    if (session.Rooms().empty())
        return false;
    const int idx = std::clamp(session.SelectedRoom(), 0, static_cast<int>(session.Rooms().size()) - 1);
    return session.Rooms()[static_cast<size_t>(idx)].hasPassword;
}

void TryJoinSelectedRoom(GameSession& session, std::string& joinPassword) {
    if (session.Rooms().empty())
        return;
    const int idx = std::clamp(session.SelectedRoom(), 0, static_cast<int>(session.Rooms().size()) - 1);
    const auto& room = session.Rooms()[static_cast<size_t>(idx)];
    session.JoinRoom(room.roomId, joinPassword);
}

void ResetGameplayState(bool& wasAlive, float& interpAlpha, bool& predictionInit, WorldRenderer& world,
                        int& hotbarSlot) {
    wasAlive = false;
    interpAlpha = 1.f;
    predictionInit = false;
    hotbarSlot = static_cast<int>(Protocol::HotbarSlot::Pistol);
    world.ResetVisibility();
}

} // namespace

Application::Application() = default;
Application::~Application() = default;

void Application::Run() {
    constexpr sf::Vector2u kSize{1280, 720};
    WorldRenderer world;
    world.Init(kSize);

    UiSystem ui;
    if (!ui.Init(world.Window())) {
        return;
    }

    GameSession session;
    InputHandler input(world.Window());
    auto& accounts = AccountStorage::Instance();
    UiFormState form;

    auto last = std::chrono::steady_clock::now();
    while (world.Window().isOpen()) {
        const auto now = std::chrono::steady_clock::now();
        const float dt = std::chrono::duration<float>(now - last).count();
        last = now;

        session.PollNetwork();
        session.Update(dt);

        const Scene scene = session.Scenes().Current();
        InputMode mode = InputMode::Menu;
        if (scene == Scene::Playing)
            mode = InputMode::Gameplay;

        sf::Vector2f aimOrigin;
        if (scene == Scene::Playing) {
            if (predictionInit_) {
                aimOrigin = {predictedX_, predictedY_};
            } else {
                auto [_, latest] = session.Snapshots();
                if (latest) {
                    for (const auto& p : latest->players) {
                        if (p.id == session.PlayerId() && p.alive) {
                            aimOrigin = {p.x, p.y};
                            break;
                        }
                    }
                }
            }
        }

        const InputState in = input.Poll(mode, ui.WantsKeyboard(), ui.WantsMouse(),
                                         scene == Scene::Playing ? &world.GetCamera() : nullptr, aimOrigin);

        if (in.quit && scene == Scene::MainMenu) {
            world.Window().close();
            continue;
        }

        ui.BeginFrame(world.Window(), dt);

        if (scene == Scene::MainMenu) {
            world.Window().clear({12, 14, 20});
            int accountSel = accounts.SelectedIndex();
            switch (ui.DrawMainMenu(session.Points(), accounts.Accounts(), accountSel, session.Error())) {
            case UiAction::SelectAccount:
                accounts.Select(accountSel);
                if (const auto* sel = accounts.SelectedAccount())
                    ApplySavedAccount(form.username, form.password, *sel);
                session.ClearError();
                session.Login(Protocol::DefaultServerHost, form.username, form.password);
                break;
            case UiAction::GoLogin:
                session.ClearError();
                if (const auto* sel = accounts.SelectedAccount())
                    ApplySavedAccount(form.username, form.password, *sel);
                session.Scenes().GoTo(Scene::Login);
                break;
            case UiAction::GoRegister:
                session.Scenes().GoTo(Scene::Register);
                break;
            case UiAction::GoSettings:
                session.Scenes().GoTo(Scene::Settings);
                break;
            case UiAction::Quit:
                world.Window().close();
                break;
            default:
                break;
            }
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::Settings) {
            world.Window().clear({12, 14, 20});
            switch (ui.DrawSettings(vsync_)) {
            case UiAction::ToggleVsync:
                world.Window().setVerticalSyncEnabled(vsync_);
                break;
            case UiAction::GoBack:
                session.Scenes().GoTo(session.Scenes().Previous());
                break;
            default:
                break;
            }
            if (in.cancel)
                session.Scenes().GoTo(session.Scenes().Previous());
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::Register) {
            world.Window().clear({12, 14, 20});
            switch (ui.DrawRegister(form.username, form.password, session.Error())) {
            case UiAction::CreateAccount:
                session.ClearError();
                session.RegisterAccount(Protocol::DefaultServerHost, form.username, form.password);
                break;
            case UiAction::GoBack:
                session.Scenes().GoTo(Scene::MainMenu);
                break;
            default:
                break;
            }
            if (in.cancel)
                session.Scenes().GoTo(Scene::MainMenu);
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::Login) {
            world.Window().clear({12, 14, 20});
            int accountSel = accounts.SelectedIndex();
            switch (ui.DrawLogin(form.username, form.password, accounts.Accounts(), accountSel, session.Error())) {
            case UiAction::SelectAccount:
                accounts.Select(accountSel);
                if (const auto* sel = accounts.SelectedAccount())
                    ApplySavedAccount(form.username, form.password, *sel);
                break;
            case UiAction::Connect:
                session.ClearError();
                session.Login(Protocol::DefaultServerHost, form.username, form.password);
                break;
            case UiAction::GoBack:
                session.Scenes().GoTo(Scene::MainMenu);
                break;
            default:
                break;
            }
            if (in.cancel)
                session.Scenes().GoTo(Scene::MainMenu);
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::RoomList) {
            world.Window().clear({12, 14, 20});
            const bool needsPassword = SelectedRoomNeedsPassword(session);
            int roomSel = session.SelectedRoom();
            switch (ui.DrawRoomList(session.Rooms(), roomSel, session.Points(), form.joinPassword, needsPassword)) {
            case UiAction::CreateRoom:
                session.CreateRoom(form.roomName.empty() ? "Squad" : form.roomName, form.roomPassword);
                break;
            case UiAction::JoinRoom:
                TryJoinSelectedRoom(session, form.joinPassword);
                break;
            case UiAction::Disconnect:
                session.Disconnect();
                break;
            default:
                break;
            }
            session.SelectRoom(roomSel);
            if (in.cancel)
                session.Disconnect();
            if (!session.Error().empty())
                ui.DrawNotification(session.Error());
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::Lobby) {
            world.Window().clear({12, 14, 20});
            bool allReady = session.AllLobbyReady(*session.Lobby());
            UiAction action = ui.DrawLobby(*session.Lobby(), session.PlayerId(), session.IsHost(), allReady, session.Error());
            if (action == UiAction::LeaveLobby)
                session.LeaveRoom();
            if (action == UiAction::StartMatch)
                session.StartMatch();
            if (action == UiAction::ToggleReady)
                session.ToggleReady();
            if (in.cancel)
                session.LeaveRoom();
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::Loading) {
            world.Window().clear({12, 14, 20});
            if (session.MapReady())
                world.SetMap(session.MapTiles(), session.MapWidth(), session.MapHeight());
            ui.DrawLoading(session.LoadingProgress());
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        if (scene == Scene::Playing || scene == Scene::Pause) {
            if (scene == Scene::Pause) {
                switch (ui.DrawPause()) {
                case UiAction::Resume:
                    session.Scenes().GoTo(Scene::Playing);
                    break;
                case UiAction::GoSettings:
                    session.Scenes().GoTo(Scene::Settings);
                    break;
                case UiAction::Quit:
                    session.EndGameplaySession();
                    ResetGameplayState(wasAlive_, interpAlpha_, predictionInit_, world, hotbarSlot_);
                    break;
                default:
                    break;
                }
            }
            if (in.cancel) {
                if (scene == Scene::Playing)
                    session.Scenes().GoTo(Scene::Pause);
                else
                    session.Scenes().GoTo(Scene::Playing);
            }

            if (scene == Scene::Playing && session.Udp()) {
                if (in.hotbarSlot >= 0)
                    hotbarSlot_ = in.hotbarSlot;

                session.Udp()->SendInput(in.moveX, in.moveY, in.aimAngle, static_cast<uint8_t>(hotbarSlot_), in.fire,
                                         in.pickup);
                LocalPrediction::ApplyMovement(predictedX_, predictedY_, in.moveX, in.moveY, dt);
                predictedAngle_ = in.aimAngle;
            }

            auto [prev, latest] = session.Snapshots();
            Snapshot renderSnap;
            if (latest) {
                interpAlpha_ = std::min(1.f, interpAlpha_ + dt * static_cast<float>(Protocol::ServerTickRate));
                if (prev && interpAlpha_ < 1.f)
                    renderSnap = Interpolation::Blend(*prev, *latest, interpAlpha_, session.PlayerId());
                else {
                    renderSnap = *latest;
                    interpAlpha_ = 0.f;
                }
            }

            const PlayerEntry* me = nullptr;
            for (const auto& p : renderSnap.players) {
                if (p.id == session.PlayerId()) {
                    me = &p;
                    break;
                }
            }

            if (me && me->alive) {
                if (!predictionInit_) {
                    predictedX_ = me->x;
                    predictedY_ = me->y;
                    predictedAngle_ = me->angle;
                    hotbarSlot_ = static_cast<int>(me->hotbarSlot);
                    predictionInit_ = true;
                } else if (latest) {
                    for (const auto& p : latest->players) {
                        if (p.id == session.PlayerId()) {
                            LocalPrediction::Reconcile(predictedX_, predictedY_, predictedAngle_, p.x, p.y, p.angle);
                            break;
                        }
                    }
                }
            }

            if (scene == Scene::Playing && me) {
                if (wasAlive_ && !me->alive) {
                    session.EndGameplaySession("You died");
                    ResetGameplayState(wasAlive_, interpAlpha_, predictionInit_, world, hotbarSlot_);
                    ui.EndFrame(world.Window());
                    world.EndFrame();
                    continue;
                }
                wasAlive_ = me->alive;
            }

            world.BeginFrame();
            if (latest) {
                std::optional<sf::Vector2f> localPos;
                if (predictionInit_ && scene == Scene::Playing && me && me->alive)
                    localPos = sf::Vector2f{predictedX_, predictedY_};
                world.RenderWorld(renderSnap, session.PlayerId(), localPos);
            }

            if (me)
                ui.DrawHud(me, session.Hotbar(), hotbarSlot_);
            if (!session.Error().empty())
                ui.DrawNotification(session.Error());
            ui.EndFrame(world.Window());
            world.EndFrame();
            continue;
        }

        world.BeginFrame();
        ui.EndFrame(world.Window());
        world.EndFrame();
    }

    ui.Shutdown(world.Window());
    session.Disconnect();
}

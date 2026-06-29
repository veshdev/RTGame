#include "UiSystem.h"

#include "Protocol.h"

#include <imgui-SFML.h>
#include <imgui.h>
#include <imgui_stdlib.h>

#include <algorithm>
#include <cmath>
#include <filesystem>
#include <cctype>
#include "ResourceManager.h"

static ResourceManager resources_; // simple process-wide resource cache

namespace {

    constexpr float kBaseWidth = 1280.f;
    constexpr float kBaseHeight = 720.f;
    constexpr float kBaseFontSize = 18.f;

    void CenteredText(const char* text) {
        const float w = ImGui::CalcTextSize(text).x;
        ImGui::SetCursorPosX((ImGui::GetWindowSize().x - w) * 0.5f);
        ImGui::TextUnformatted(text);
    }

    bool CenteredButton(const char* label, const ImVec2& size) {
        ImGui::SetCursorPosX((ImGui::GetWindowSize().x - size.x) * 0.5f);
        return ImGui::Button(label, size);
    }

} // namespace

bool UiSystem::Init(sf::RenderWindow& window) {
    if (initialized_)
        return true;

    viewport_ = window.getSize();
    if (!ImGui::SFML::Init(window))
        return false;

    if (!SetupFonts()) {
        ImGui::SFML::Shutdown(window);
        return false;
    }

    ApplyTheme();
    initialized_ = true;
    return true;
}

void UiSystem::Shutdown(sf::RenderWindow& window) {
    if (!initialized_)
        return;
    ImGui::SFML::Shutdown(window);
    initialized_ = false;
}

bool UiSystem::SetupFonts()
{
    ImGuiIO& io = ImGui::GetIO();

    ImFontConfig cfg;
    cfg.SizePixels = kBaseFontSize;

    io.FontDefault = io.Fonts->AddFontDefaultVector(&cfg);

    if (!io.FontDefault)
        return false;

    io.Fonts->Build();
    return true;
}

void UiSystem::ApplyTheme() {
    ImGuiStyle& style = ImGui::GetStyle();
    const float s = UiScale();

    style.ScaleAllSizes(s);
    style.FontSizeBase = kBaseFontSize * s;
    style.WindowRounding = 6.f * s;
    style.FrameRounding = 4.f * s;
    style.ScrollbarSize = 12.f * s;

    ImVec4* colors = style.Colors;
    colors[ImGuiCol_WindowBg] = { 0.08f, 0.09f, 0.12f, 0.97f };
    colors[ImGuiCol_ChildBg] = { 0.11f, 0.12f, 0.16f, 1.f };
    colors[ImGuiCol_FrameBg] = { 0.14f, 0.16f, 0.22f, 1.f };
    colors[ImGuiCol_Button] = { 0.20f, 0.35f, 0.55f, 1.f };
    colors[ImGuiCol_ButtonHovered] = { 0.28f, 0.45f, 0.68f, 1.f };
    colors[ImGuiCol_ButtonActive] = { 0.18f, 0.30f, 0.48f, 1.f };
    colors[ImGuiCol_Header] = { 0.22f, 0.38f, 0.58f, 0.55f };
    colors[ImGuiCol_Text] = { 0.92f, 0.93f, 0.96f, 1.f };
}

void UiSystem::BeginFrame(sf::RenderWindow& window, float dt) {
    if (!initialized_)
        return;

    viewport_ = window.getSize();
    ImGui::SFML::Update(window, sf::seconds(dt));
}

void UiSystem::EndFrame(sf::RenderWindow& window) {
    if (!initialized_)
        return;
    ImGui::SFML::Render(window);
}

bool UiSystem::WantsKeyboard() const {
    return ImGui::GetIO().WantCaptureKeyboard;
}

bool UiSystem::WantsMouse() const {
    return ImGui::GetIO().WantCaptureMouse;
}

float UiSystem::UiScale() const {
    const float sx = viewport_.x / kBaseWidth;
    const float sy = viewport_.y / kBaseHeight;
    return std::clamp(std::min(sx, sy), 0.75f, 2.f);
}

void UiSystem::BeginScreen(const char* id) {
    const ImGuiViewport* vp = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos(vp->WorkPos);
    ImGui::SetNextWindowSize(vp->WorkSize);
    ImGui::Begin(id, nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoBringToFrontOnFocus | ImGuiWindowFlags_NoNav);
}

void UiSystem::EndScreen() {
    ImGui::End();
}

UiAction UiSystem::DrawMainMenu(int points, const std::vector<SavedAccount>& accounts, int& selectedAccount,
    const std::string& notice) {
    BeginScreen("MainMenu");
    UiAction action = UiAction::None;

    ImGui::Spacing();
    CenteredText("REALTIME GAME CLIENT");
    ImGui::Spacing();
    ImGui::Text("Points: %d", points);
    ImGui::Separator();
    ImGui::Spacing();

    const float entryH = 18.f * UiScale();
    const float listH = std::min(270.f * UiScale(), entryH * static_cast<float>(accounts.size()) + 54.f); // 270 - max size of accounts list
    if (ImGui::BeginChild("Accounts", { 0.f, listH }, true)) {
        ImGui::TextDisabled("Saved Accounts");
        for (size_t i = 0; i < accounts.size(); ++i) {
            const bool sel = static_cast<int>(i) == selectedAccount;
            ImGui::PushID(static_cast<int>(i));
            if (ImGui::Selectable(accounts[i].username.c_str(), sel, 0, { 0.f, entryH })) {
                selectedAccount = static_cast<int>(i);
                action = UiAction::SelectAccount;
            }
            ImGui::PopID();
        }
    }
    ImGui::EndChild();

    ImGui::Spacing();
    const ImVec2 btn{ 220.f * UiScale(), 36.f * UiScale() };
    if (CenteredButton("Login", btn))
        action = UiAction::GoLogin;
    if (CenteredButton("Register", btn))
        action = UiAction::GoRegister;
    if (CenteredButton("Settings", btn))
        action = UiAction::GoSettings;
    if (CenteredButton("Quit", btn))
        action = UiAction::Quit;

    if (!notice.empty()) {
        ImGui::Spacing();
        ImGui::TextColored({ 0.4f, 0.85f, 0.5f, 1.f }, "%s", notice.c_str());
    }

    EndScreen();
    return action;
}

UiAction UiSystem::DrawLogin(std::string& username, std::string& password,
    const std::vector<SavedAccount>& accounts, int& selectedAccount,
    const std::string& error) {
    BeginScreen("Login");
    UiAction action = UiAction::None;

    CenteredText("LOGIN");
    ImGui::Spacing();
    ImGui::TextDisabled("Server: %s", Protocol::DefaultServerHost);

    ImGui::InputText("Username", &username);
    ImGui::InputText("Password", &password, ImGuiInputTextFlags_Password);

    // Фиксированная высота 200 пикселей для списка аккаунтов
    if (ImGui::BeginChild("SavedLogin", { 0.f, 200.f }, true)) {
        for (size_t i = 0; i < accounts.size() && i < 6; ++i) {
            if (ImGui::Selectable(accounts[i].username.c_str(), static_cast<int>(i) == selectedAccount)) {
                selectedAccount = static_cast<int>(i);
                action = UiAction::SelectAccount;
            }
        }
    }
    ImGui::EndChild();

    const ImVec2 btn{ 200.f * UiScale(), 36.f * UiScale() };
    if (CenteredButton("Connect", btn))
        action = UiAction::Connect;
    if (CenteredButton("Back", btn))
        action = UiAction::GoBack;

    if (!error.empty())
        ImGui::TextColored({ 0.9f, 0.35f, 0.35f, 1.f }, "%s", error.c_str());

    EndScreen();
    return action;
}

UiAction UiSystem::DrawRegister(std::string& username, std::string& password, const std::string& error) {
    BeginScreen("Register");
    UiAction action = UiAction::None;

    CenteredText("REGISTER");
    ImGui::Spacing();
    ImGui::TextDisabled("Server: %s", Protocol::DefaultServerHost);

    ImGui::InputText("Username", &username);
    ImGui::InputText("Password", &password, ImGuiInputTextFlags_Password);

    const ImVec2 btn{ 200.f * UiScale(), 36.f * UiScale() };
    if (CenteredButton("Create Account", btn))
        action = UiAction::CreateAccount;
    if (CenteredButton("Back", btn))
        action = UiAction::GoBack;

    if (!error.empty())
        ImGui::TextColored({ 0.9f, 0.35f, 0.35f, 1.f }, "%s", error.c_str());

    EndScreen();
    return action;
}

UiAction UiSystem::DrawRoomList(const std::vector<RoomListEntry>& rooms, int& selected, int points,
    std::string& joinPassword, bool showPasswordField) {
    BeginScreen("RoomList");
    UiAction action = UiAction::None;

    CenteredText("SERVERS");
    ImGui::SameLine(ImGui::GetWindowWidth() - 160.f * UiScale());
    ImGui::Text("Points: %d", points);
    ImGui::Separator();

    const float tableH = (showPasswordField ? 340.f : 400.f) * UiScale();
    if (ImGui::BeginChild("RoomTable", { 0.f, tableH }, true)) {
        for (size_t i = 0; i < rooms.size(); ++i) {
            const auto& r = rooms[i];
            std::string label = r.name;
            if (r.hasPassword)
                label += "  [locked]";
            label += "  (" + std::to_string(r.playerCount) + "/4)";
            if (ImGui::Selectable(label.c_str(), static_cast<int>(i) == selected))
                selected = static_cast<int>(i);
        }
    }
    ImGui::EndChild();

    if (showPasswordField)
        ImGui::InputText("Room password", &joinPassword, ImGuiInputTextFlags_Password);

    const ImVec2 roomBtn{ 140.f * UiScale(), 36.f * UiScale() };
    if (ImGui::Button("Create", roomBtn))
        action = UiAction::CreateRoom;
    ImGui::SameLine();
    if (ImGui::Button("Join", roomBtn))
        action = UiAction::JoinRoom;
    ImGui::SameLine();
    if (ImGui::Button("Disconnect", { 180.f * UiScale(), 36.f * UiScale() }))
        action = UiAction::Disconnect;

    EndScreen();
    return action;
}

UiAction UiSystem::DrawLobby(const LobbyState& lobby, uint8_t myPid, bool isHost, bool allReady, const std::string& error) {
    BeginScreen("Lobby");
    UiAction action = UiAction::None;

    CenteredText("LOBBY");
    ImGui::TextDisabled(isHost ? "Waiting for players..." : "Preparing match...");
    ImGui::Separator();

    if (ImGui::BeginChild("LobbySlots", { 0.f, 300.f * UiScale() }, true)) {
        for (const auto& s : lobby.slots) {
            std::string line;
            if (s.playerId == lobby.hostPlayerId)
                line += "[HOST] ";
            line += s.username;
            if (s.playerId == myPid)
                line += " (you)";
            line += s.ready ? "  READY" : "  ...";
            ImGui::TextUnformatted(line.c_str());
        }
    }
    ImGui::EndChild();

    // Find current player's ready status
    bool myReady = false;
    for (const auto& s : lobby.slots) {
        if (s.playerId == myPid) {
            myReady = s.ready;
            break;
        }
    }

    // Toggle Ready button
    const char* readyButtonLabel = myReady ? "Not Ready" : "Toggle Ready";
    if (CenteredButton(readyButtonLabel, { 180.f * UiScale(), 40.f * UiScale() }))
        action = UiAction::ToggleReady;

    if (isHost && allReady) {
        if (CenteredButton("Start", { 180.f * UiScale(), 40.f * UiScale() }))
            action = UiAction::StartMatch;
    }

    if (CenteredButton("Leave", { 180.f * UiScale(), 40.f * UiScale() }))
        action = UiAction::LeaveLobby;

    if (!error.empty())
        ImGui::TextColored({ 0.9f, 0.35f, 0.35f, 1.f }, "%s", error.c_str());

    EndScreen();
    return action;
}

void UiSystem::DrawLoading(float progress) {
    BeginScreen("Loading");
    CenteredText("Deploying...");
    ImGui::Spacing();
    const float barW = std::min(440.f * UiScale(), ImGui::GetWindowWidth() - 80.f * UiScale());
    ImGui::SetCursorPosX((ImGui::GetWindowSize().x - barW) * 0.5f);
    ImGui::ProgressBar(progress, { barW, 28.f * UiScale() });
    EndScreen();
}

void UiSystem::DrawHud(const PlayerEntry* player, const std::vector<HotbarSlot>& hotbar, int selectedSlot) {
    if (!player)
        return;

    const float scale = UiScale();
    const ImGuiViewport* vp = ImGui::GetMainViewport();
    const float margin = 16.f * scale;
    const float statusH = 100.f * scale;

    ImGui::SetNextWindowPos({ vp->WorkPos.x + margin, vp->WorkPos.y + vp->WorkSize.y - statusH - margin });
    ImGui::SetNextWindowSize({ 320.f * scale, statusH });
    ImGui::Begin("HUD_Status", nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoNav | ImGuiWindowFlags_NoInputs);
    const float hpFrac = std::clamp(player->hp / static_cast<float>(Protocol::Entity::PlayerMaxHp), 0.f, 1.f);
    ImGui::ProgressBar(hpFrac, { -1.f, 22.f * scale }, ("HP " + std::to_string(player->hp)).c_str());
    ImGui::Text("Loot: %d", player->carriedLoot);
    if (player->extractionProgress > 0)
        ImGui::ProgressBar(player->extractionProgress / 100.f, { -1.f, 18.f * scale }, "Extracting");
    ImGui::End();

    const float slotSize = 64.f * scale;
    const float slotGap = 4.f * scale;
    const float hotbarW = slotSize * Protocol::HotbarSlots + slotGap * (Protocol::HotbarSlots - 1);
    const float hotbarH = slotSize + 6.f * scale;
    ImGui::SetNextWindowPos(
        { vp->WorkPos.x + vp->WorkSize.x * 0.5f - hotbarW * 0.5f, vp->WorkPos.y + vp->WorkSize.y - hotbarH - margin });
    ImGui::SetNextWindowSize({ hotbarW, hotbarH });
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0.f, 0.f));
    ImGui::Begin("HUD_Hotbar", nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoNav | ImGuiWindowFlags_NoInputs);
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(2.f * scale, 2.f * scale));
    ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(slotGap, 2.f * scale));
    for (unsigned i = 0; i < Protocol::HotbarSlots; ++i) {
        if (i > 0) {
            ImGui::SameLine(0.f, slotGap);
        }
        const bool active = selectedSlot == static_cast<int>(i);
        if (active)
            ImGui::PushStyleColor(ImGuiCol_Button, { 0.25f, 0.45f, 0.30f, 1.f });
        ImGui::PushID(static_cast<int>(i));
        auto IconPathForIndex = [](unsigned idx) -> std::string {
            switch (idx) {
                case 0: return "assets/icons/axe.png";
                case 1: return "assets/icons/pistol.png";
                case 2: return "assets/icons/rifle.png";
                case 3: return "assets/icons/shotgun.png";
                case 4: return "assets/icons/pistol_mag.png";
                case 5: return "assets/icons/rifle_mag.png";
                case 6: return "assets/icons/shotgun_ammo.png";
                case 7: return "assets/icons/medkit.png";
                case 8: return "assets/icons/loot.png";
                default: return "";
            }
        };
        std::string iconPath = IconPathForIndex(i);
        auto tex = iconPath.empty() ? nullptr : resources_.Texture(iconPath);
        if (tex) {
            if (active) {
                ImVec2 p = ImGui::GetCursorScreenPos();
                ImGui::GetWindowDrawList()->AddRectFilled(p, { p.x + slotSize, p.y + slotSize }, IM_COL32(60, 115, 76, 255));
            }
            ImTextureID tid = (ImTextureID)(intptr_t)tex->getNativeHandle();
            ImGui::Image(tid, { slotSize, slotSize });
            if (i >= 4 && i < hotbar.size() && hotbar[i].quantity > 0) {
                ImVec2 a = ImGui::GetItemRectMin();
                ImVec2 b = ImGui::GetItemRectMax();
                std::string q = std::to_string(hotbar[i].quantity);
                ImU32 col = IM_COL32(255,255,255,255);
                ImGui::GetWindowDrawList()->AddText(ImGui::GetFont(), ImGui::GetFontSize()*1.3f, ImVec2(b.x - 6.f - ImGui::CalcTextSize(q.c_str()).x, b.y - 12.f), col, q.c_str());
            }
        } else {
            std::string label = Protocol::HotbarSlotName(static_cast<uint8_t>(i));
            if (i >= 4 && i < hotbar.size() && hotbar[i].quantity > 0)
                label += "\n" + std::to_string(hotbar[i].quantity);
            ImGui::Button(label.c_str(), { slotSize, slotSize });
        }
        ImGui::PopID();
        if (active)
            ImGui::PopStyleColor();
    }
    ImGui::PopStyleVar(2);
    ImGui::End();
    ImGui::PopStyleVar();

    const bool medkitSelected = selectedSlot == static_cast<int>(Protocol::HotbarSlot::Medkit);
    ImGui::SetNextWindowPos({ vp->WorkPos.x + margin, vp->WorkPos.y + vp->WorkSize.y - statusH - hotbarH - margin * 2.f });
    ImGui::SetNextWindowBgAlpha(0.f);
    ImGui::Begin("HUD_Help", nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoInputs |
        ImGuiWindowFlags_NoNav | ImGuiWindowFlags_NoBackground);
    ImGui::TextDisabled("%s | 1-9 Select | E Pickup | ESC Pause",
        medkitSelected ? "LMB Use Medkit" : "LMB Shoot");
    ImGui::End();
}

UiAction UiSystem::DrawPause() {
    const ImGuiViewport* vp = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos(vp->WorkPos);
    ImGui::SetNextWindowSize(vp->WorkSize);
    ImGui::PushStyleColor(ImGuiCol_WindowBg, { 0.f, 0.f, 0.f, 0.55f });
    ImGui::Begin("PauseOverlay", nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoNav);

    UiAction action = UiAction::None;
    const ImVec2 panel{ 320.f * UiScale(), 260.f * UiScale() };
    ImGui::SetCursorPos({ (vp->WorkSize.x - panel.x) * 0.5f, (vp->WorkSize.y - panel.y) * 0.5f });
    ImGui::BeginChild("PausePanel", panel, true);
    CenteredText("PAUSED");
    ImGui::Spacing();
    const ImVec2 pauseBtn{ 220.f * UiScale(), 40.f * UiScale() };
    if (CenteredButton("Resume", pauseBtn))
        action = UiAction::Resume;
    if (CenteredButton("Settings", pauseBtn))
        action = UiAction::GoSettings;
    if (CenteredButton("Quit to Menu", pauseBtn))
        action = UiAction::Quit;
    ImGui::EndChild();

    ImGui::End();
    ImGui::PopStyleColor();
    return action;
}

UiAction UiSystem::DrawSettings(bool& vsync) {
    BeginScreen("Settings");
    UiAction action = UiAction::None;

    CenteredText("SETTINGS");
    ImGui::Spacing();
    if (ImGui::Checkbox("VSync", &vsync))
        action = UiAction::ToggleVsync;

    if (CenteredButton("Back", { 200.f, 36.f }))
        action = UiAction::GoBack;

    EndScreen();
    return action;
}

void UiSystem::DrawNotification(const std::string& text) {
    if (text.empty())
        return;
    const ImGuiViewport* vp = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos({ vp->WorkPos.x + 16.f, vp->WorkPos.y + 16.f });
    ImGui::SetNextWindowSize({ vp->WorkSize.x - 32.f * UiScale(), 48.f * UiScale() });
    ImGui::Begin("Notification", nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoNav | ImGuiWindowFlags_NoInputs);
    ImGui::TextUnformatted(text.c_str());
    ImGui::End();
}

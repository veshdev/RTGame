#pragma once

#include "Entities.h"
#include "AccountStorage.h"
#include <SFML/Graphics/RenderWindow.hpp>
#include <string>
#include <vector>

enum class UiAction {
    None,
    Quit,
    GoLogin,
    GoRegister,
    GoSettings,
    GoBack,
    SelectAccount,
    Connect,
    CreateAccount,
    CreateRoom,
    JoinRoom,
    Disconnect,
    LeaveLobby,
    ToggleReady,
    StartMatch,
    Resume,
    ToggleVsync,
};

struct UiFormState {
    std::string username;
    std::string password;
    std::string roomName = "Squad";
    std::string roomPassword;
    std::string joinPassword;
    int selectedAccount = -1;
    int selectedRoom = 0;
    int hotbarSlot = 1;
};

class UiSystem {
public:
    bool Init(sf::RenderWindow& window);
    void Shutdown(sf::RenderWindow& window);
    void BeginFrame(sf::RenderWindow& window, float dt);
    void EndFrame(sf::RenderWindow& window);

    bool WantsKeyboard() const;
    bool WantsMouse() const;

    UiAction DrawMainMenu(int points, const std::vector<SavedAccount>& accounts, int& selectedAccount,
                          const std::string& notice);
    UiAction DrawLogin(std::string& username, std::string& password, const std::vector<SavedAccount>& accounts,
                       int& selectedAccount, const std::string& error);
    UiAction DrawRegister(std::string& username, std::string& password, const std::string& error);
    UiAction DrawRoomList(const std::vector<RoomListEntry>& rooms, int& selected, int points,
                          std::string& joinPassword, bool showPasswordField);
    UiAction DrawLobby(const LobbyState& lobby, uint8_t myPid, bool isHost, bool allReady, const std::string& error);
    void DrawLoading(float progress);
    void DrawHud(const PlayerEntry* player, const std::vector<HotbarSlot>& hotbar, int selectedSlot);
    UiAction DrawPause();
    UiAction DrawSettings(bool& vsync);
    void DrawNotification(const std::string& text);

private:
    bool SetupFonts();
    void ApplyTheme();
    void BeginScreen(const char* id);
    void EndScreen();
    float UiScale() const;

    bool initialized_ = false;
    sf::Vector2u viewport_{1280, 720};
};

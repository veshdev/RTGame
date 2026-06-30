#pragma once

#include "Camera.h"
#include <SFML/Window/Window.hpp>
#include <optional>
#include <string>

enum class InputMode {
    Menu,
    Text,
    Gameplay
};

struct InputState {
    bool quit = false;
    bool confirm = false;
    bool cancel = false;
    bool tab = false;
    bool refresh = false;
    bool toggleReady = false;
    bool openSettings = false;
    bool toggleVsync = false;
    bool toggleFps = false;
    bool createRoom = false;
    bool joinRoom = false;
    bool up = false;
    bool down = false;

    sf::Vector2i mousePos{};
    bool mouseClick = false;

    float moveX = 0.f;
    float moveY = 0.f;
    float aimAngle = 0.f;
    bool fire = false;
    bool pickup = false;
    int hotbarSlot = -1;
    int hotbarDelta = 0;

    std::optional<char> textChar;
};

class InputHandler {
public:
    explicit InputHandler(sf::Window& window);

    InputState Poll(InputMode mode, bool uiWantsKeyboard, bool uiWantsMouse, const Camera* camera = nullptr,
                    sf::Vector2f aimOrigin = {});

private:
    sf::Window& window_;
    bool focused_ = true;
};

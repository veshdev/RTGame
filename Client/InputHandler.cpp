#include "InputHandler.h"

#include <imgui-SFML.h>
#include <imgui.h>

#include <SFML/Window/Keyboard.hpp>
#include <SFML/Window/Mouse.hpp>

#include <cmath>

InputHandler::InputHandler(sf::Window& window) : window_(window) {}

InputState InputHandler::Poll(InputMode mode, bool uiWantsKeyboard, bool uiWantsMouse, const Camera* camera,
                              sf::Vector2f aimOrigin) {
    InputState state;

    // Always poll events so we can handle window close and focus events even when unfocused.
    state.mousePos = sf::Mouse::getPosition(window_);

    while (const auto event = window_.pollEvent()) {
        if (ImGui::GetCurrentContext() != nullptr)
            ImGui::SFML::ProcessEvent(window_, *event);

        if (event->is<sf::Event::Closed>()) {
            // Ensure clicking the window close button actually closes the application.
            window_.close();
            state.quit = true;
        } else if (event->is<sf::Event::FocusLost>()) {
            focused_ = false;
        } else if (event->is<sf::Event::FocusGained>()) {
            focused_ = true;
        } else if (const auto* key = event->getIf<sf::Event::KeyPressed>()) {
            // Skip processing keyboard input if window isn't focused or UI wants keyboard
            if (!focused_ || uiWantsKeyboard)
                continue;
            using Key = sf::Keyboard::Key;
            switch (key->code) {
            case Key::Escape:
                state.cancel = true;
                break;
            case Key::Enter:
                state.confirm = true;
                break;
            case Key::Tab:
                state.tab = true;
                break;
            case Key::Space:
                if (mode != InputMode::Gameplay)
                    state.toggleReady = true;
                break;
            case Key::E:
                if (mode == InputMode::Gameplay)
                    state.pickup = true;
                break;
            case Key::V:
                state.toggleVsync = true;
                break;
            case Key::F3:
                state.toggleFps = true;
                break;
            case Key::Q:
                state.quit = true;
                break;
            case Key::Up:
                state.up = true;
                break;
            case Key::Down:
                state.down = true;
                break;
            case Key::Num1:
            case Key::Numpad1:
                state.hotbarSlot = 0;
                break;
            case Key::Num2:
            case Key::Numpad2:
                state.hotbarSlot = 1;
                break;
            case Key::Num3:
            case Key::Numpad3:
                state.hotbarSlot = 2;
                break;
            case Key::Num4:
            case Key::Numpad4:
                state.hotbarSlot = 3;
                break;
            case Key::Num5:
            case Key::Numpad5:
                state.hotbarSlot = 4;
                break;
            case Key::Num6:
            case Key::Numpad6:
                state.hotbarSlot = 5;
                break;
            case Key::Num7:
            case Key::Numpad7:
                state.hotbarSlot = 6;
                break;
            case Key::Num8:
            case Key::Numpad8:
                state.hotbarSlot = 7;
                break;
            case Key::Num9:
            case Key::Numpad9:
                state.hotbarSlot = 8;
                break;
            default:
                break;
            }
        } else if (const auto* mouse = event->getIf<sf::Event::MouseButtonPressed>()) {
            // Skip mouse clicks when unfocused or when UI wants the mouse
            if (!focused_ || uiWantsMouse)
                continue;
            if (mouse->button == sf::Mouse::Button::Left)
                state.mouseClick = true;
        } else if (const auto* wheel = event->getIf<sf::Event::MouseWheelScrolled>()) {
            // Skip wheel input when unfocused or when UI wants the mouse
            if (!focused_ || uiWantsMouse)
                continue;
            // SFML: wheel->delta > 0 means scroll up. We'll map scroll up to previous slot (-1), down to next (+1).
            state.hotbarDelta -= static_cast<int>(wheel->delta);
        }
    }

    // If not focused, avoid reading continuous keyboard/mouse state; events above still update focus.
    if (mode == InputMode::Gameplay && focused_ && !uiWantsKeyboard) {
        using Key = sf::Keyboard::Key;
        if (sf::Keyboard::isKeyPressed(Key::W))
            state.moveY -= 1.f;
        if (sf::Keyboard::isKeyPressed(Key::S))
            state.moveY += 1.f;
        if (sf::Keyboard::isKeyPressed(Key::A))
            state.moveX -= 1.f;
        if (sf::Keyboard::isKeyPressed(Key::D))
            state.moveX += 1.f;

        if (!uiWantsMouse && sf::Mouse::isButtonPressed(sf::Mouse::Button::Left))
            state.fire = true;

        if (camera) {
            const sf::Vector2f mouseWorld = camera->ScreenToWorld(state.mousePos);
            const float dx = mouseWorld.x - aimOrigin.x;
            const float dy = mouseWorld.y - aimOrigin.y;
            float ang = std::atan2(dx, -dy) / (2.f * 3.14159265f);
            if (ang < 0.f)
                ang += 1.f;
            state.aimAngle = ang;
        }
    }

    return state;
}

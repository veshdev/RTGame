#include "Camera.h"
#include <algorithm>

void Camera::SetViewport(sf::Vector2u size) {
    viewport_ = size;
}

void Camera::Follow(float worldX, float worldY) {
    const float halfW = static_cast<float>(viewport_.x) * 0.5f;
    const float halfH = static_cast<float>(viewport_.y) * 0.5f;
    const float mapW = static_cast<float>(Protocol::MapW * Protocol::TileSize);
    const float mapH = static_cast<float>(Protocol::MapH * Protocol::TileSize);
    center_.x = std::clamp(worldX, halfW, std::max(halfW, mapW - halfW));
    center_.y = std::clamp(worldY, halfH, std::max(halfH, mapH - halfH));
}

void Camera::Apply(sf::RenderWindow& window) const {
    sf::View view;
    view.setCenter(center_);
    view.setSize({static_cast<float>(viewport_.x), static_cast<float>(viewport_.y)});
    window.setView(view);
}

void Camera::ResetUi(sf::RenderWindow& window) const {
    sf::View view;
    view.setCenter({viewport_.x * 0.5f, viewport_.y * 0.5f});
    view.setSize({static_cast<float>(viewport_.x), static_cast<float>(viewport_.y)});
    window.setView(view);
}

sf::Vector2f Camera::ScreenToWorld(sf::Vector2i screen) const {
    const float halfW = static_cast<float>(viewport_.x) * 0.5f;
    const float halfH = static_cast<float>(viewport_.y) * 0.5f;
    return {center_.x + (static_cast<float>(screen.x) - halfW),
            center_.y + (static_cast<float>(screen.y) - halfH)};
}

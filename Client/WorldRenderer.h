#pragma once

#include "Entities.h"
#include "Camera.h"
#include "TileMap.h"
#include "ResourceManager.h"
#include <SFML/Graphics.hpp>
#include <optional>

class WorldRenderer {
public:
    WorldRenderer();
    bool Init(sf::Vector2u size);
    void SetMap(const std::vector<uint8_t>& tiles, unsigned mapW, unsigned mapH);
    void ResetVisibility();
    void BeginFrame();
    void RenderWorld(const Snapshot& snapshot, uint8_t localPid,
                     const std::optional<sf::Vector2f>& localOverride);
    void EndFrame();

    sf::RenderWindow& Window() { return window_; }
    Camera& GetCamera() { return camera_; }

private:
    bool EntityVisible(float x, float y) const;
    void DrawPlayer(const PlayerEntry& p, uint8_t localPid);
    void DrawMonster(const MonsterEntry& m);
    void DrawProjectile(const ProjectileEntry& p);
    void DrawLoot(const LootEntry& l);
    void DrawCircle(float x, float y, float r, const sf::Color& color);
    void DrawRect(float x, float y, float w, float h, const sf::Color& color);
    void DrawDirection(float x, float y, float angle, float length, const sf::Color& color);
    void DrawHpBar(float x, float y, float w, float h, int hp, int maxHp);
    std::string IconPathForItem(uint8_t itemType) const;

    sf::RenderWindow window_;
    Camera camera_;
    TileMap tileMap_;
    sf::CircleShape circle_;
    sf::RectangleShape rect_;
    ResourceManager resources_;
};

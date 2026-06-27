#include "WorldRenderer.h"
#include "Protocol.h"
#include <algorithm>
#include <cmath>

namespace {
constexpr sf::Color kSelf{50, 200, 50};
constexpr sf::Color kOther{200, 200, 50};
constexpr sf::Color kZombie{170, 60, 60};
constexpr sf::Color kMarauder{200, 120, 40};
constexpr sf::Color kProjectile{255, 220, 50};
constexpr sf::Color kLoot{50, 180, 220};
constexpr int kViewRadiusTiles = 18;
} // namespace

WorldRenderer::WorldRenderer() {
    circle_.setPointCount(24);
    rect_.setOutlineThickness(0.f);
}

bool WorldRenderer::Init(sf::Vector2u size) {
    window_.create(sf::VideoMode(size), "Realtime Game", sf::Style::Default, sf::State::Windowed);
    window_.setFramerateLimit(120);
    window_.setVerticalSyncEnabled(true);
    camera_.SetViewport(size);
    return true;
}

void WorldRenderer::SetMap(const std::vector<uint8_t>& tiles, unsigned mapW, unsigned mapH) {
    tileMap_.Build(tiles, mapW, mapH, Protocol::TileSize);
}

void WorldRenderer::ResetVisibility() {
    tileMap_.ResetVisibility();
}

void WorldRenderer::BeginFrame() {
    window_.clear({30, 30, 30});
}

bool WorldRenderer::EntityVisible(float x, float y) const {
    return tileMap_.IsVisibleWorld(x, y);
}

void WorldRenderer::RenderWorld(const Snapshot& snapshot, uint8_t localPid,
                                const std::optional<sf::Vector2f>& localOverride) {
    sf::Vector2f viewOrigin;
    const PlayerEntry* local = nullptr;
    for (const auto& p : snapshot.players) {
        if (p.id == localPid) {
            local = &p;
            break;
        }
    }

    if (localOverride) {
        viewOrigin = *localOverride;
    } else if (local && local->alive) {
        viewOrigin = {local->x, local->y};
    }

    if (local && local->alive) {
        camera_.Follow(viewOrigin.x, viewOrigin.y);
        tileMap_.UpdateVisibility(viewOrigin.x, viewOrigin.y, kViewRadiusTiles);
    }

    camera_.Apply(window_);
    tileMap_.Draw(window_);
    tileMap_.DrawExtractionZones(window_);

    for (const auto& p : snapshot.players) {
        if (p.id == localPid) {
            if (!localOverride || !p.alive)
                continue;
            PlayerEntry drawP = p;
            drawP.x = localOverride->x;
            drawP.y = localOverride->y;
            DrawPlayer(drawP, localPid);
        } else if (EntityVisible(p.x, p.y)) {
            DrawPlayer(p, localPid);
        }
    }
    for (const auto& m : snapshot.monsters) {
        if (EntityVisible(m.x, m.y))
            DrawMonster(m);
    }
    for (const auto& pr : snapshot.projectiles) {
        if (EntityVisible(pr.x, pr.y))
            DrawProjectile(pr);
    }
    for (const auto& l : snapshot.loot) {
        if (EntityVisible(l.x, l.y))
            DrawLoot(l);
    }
}

void WorldRenderer::EndFrame() {
    window_.display();
}

void WorldRenderer::DrawPlayer(const PlayerEntry& p, uint8_t localPid) {
    if (!p.alive)
        return;
    const sf::Color color = (p.id == localPid) ? kSelf : kOther;
    DrawCircle(p.x, p.y, 12.f, color);
    DrawDirection(p.x, p.y, p.angle, 18.f, color);
    DrawHpBar(p.x - 16.f, p.y - 22.f, 32.f, 5.f, p.hp, Protocol::Entity::PlayerMaxHp);
}

void WorldRenderer::DrawMonster(const MonsterEntry& m) {
    const sf::Color color = (m.type == Protocol::MonsterType::Marauder) ? kMarauder : kZombie;
    DrawCircle(m.x, m.y, 14.f, color);
    DrawDirection(m.x, m.y, m.angle, 20.f, color);
}

void WorldRenderer::DrawProjectile(const ProjectileEntry& p) {
    DrawCircle(p.x, p.y, 4.f, kProjectile);
}

void WorldRenderer::DrawLoot(const LootEntry& l) {
    DrawRect(l.x - 6.f, l.y - 6.f, 12.f, 12.f, kLoot);
}

void WorldRenderer::DrawCircle(float x, float y, float r, const sf::Color& color) {
    circle_.setRadius(r);
    circle_.setOrigin({r, r});
    circle_.setPosition({x, y});
    circle_.setFillColor(color);
    window_.draw(circle_);
}

void WorldRenderer::DrawRect(float x, float y, float w, float h, const sf::Color& color) {
    rect_.setSize({w, h});
    rect_.setPosition({x, y});
    rect_.setFillColor(color);
    window_.draw(rect_);
}

void WorldRenderer::DrawDirection(float x, float y, float angle, float length, const sf::Color& color) {
    const float rad = angle * 2.f * 3.14159265f;
    const float ex = x + std::sin(rad) * length;
    const float ey = y - std::cos(rad) * length;
    sf::Vertex line[] = {sf::Vertex{{x, y}, color}, sf::Vertex{{ex, ey}, color}};
    window_.draw(line, 2, sf::PrimitiveType::Lines);
}

void WorldRenderer::DrawHpBar(float x, float y, float w, float h, int hp, int maxHp) {
    DrawRect(x, y, w, h, {40, 40, 40});
    const float frac = std::clamp(hp / static_cast<float>(maxHp), 0.f, 1.f);
    DrawRect(x, y, w * frac, h, {50, 200, 50});
}

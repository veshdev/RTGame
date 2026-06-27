#include "TileMap.h"
#include "Protocol.h"

#include <algorithm>
#include <cmath>

namespace {

sf::Color TileColor(uint8_t tile) {
    switch (tile) {
    case Protocol::TileType::Road:
        return {55, 55, 60};
    case Protocol::TileType::Wall:
        return {80, 80, 100};
    case Protocol::TileType::Floor:
        return {90, 70, 60};
    case Protocol::TileType::Door:
        return {120, 90, 50};
    case Protocol::TileType::Spawn:
        return {60, 90, 60};
    case Protocol::TileType::Extraction:
        return {30, 160, 220};
    default:
        return {50, 50, 50};
    }
}

sf::Color DimColor(const sf::Color& base, bool lit, bool explored) {
    if (lit)
        return base;
    if (explored)
        return {static_cast<uint8_t>(base.r / 4), static_cast<uint8_t>(base.g / 4),
                static_cast<uint8_t>(base.b / 4)};
    return {14, 14, 18};
}

} // namespace

bool TileMap::Build(const std::vector<uint8_t>& tiles, unsigned mapW, unsigned mapH, unsigned tileSize) {
    tiles_ = tiles;
    mapW_ = mapW;
    mapH_ = mapH;
    tileSize_ = tileSize;
    visible_.assign(mapW * mapH, 0);
    explored_.assign(mapW * mapH, 0);
    extractionCenters_.clear();
    vertices_.clear();
    vertices_.resize(mapW * mapH * 6);

    for (unsigned y = 0; y < mapH; ++y) {
        for (unsigned x = 0; x < mapW; ++x) {
            const size_t idx = y * mapW + x;
            const uint8_t tile = (idx < tiles.size()) ? tiles[idx] : Protocol::TileType::Wall;
            if (tile == Protocol::TileType::Extraction) {
                extractionCenters_.push_back(
                    {x * tileSize + tileSize * 0.5f, y * tileSize + tileSize * 0.5f});
            }

            const size_t base = idx * 6;
            const float fx = static_cast<float>(x * tileSize);
            const float fy = static_cast<float>(y * tileSize);
            const float ts = static_cast<float>(tileSize);

            vertices_[base + 0].position = {fx, fy};
            vertices_[base + 1].position = {fx + ts, fy};
            vertices_[base + 2].position = {fx + ts, fy + ts};
            vertices_[base + 3].position = {fx, fy};
            vertices_[base + 4].position = {fx + ts, fy + ts};
            vertices_[base + 5].position = {fx, fy + ts};
        }
    }
    RefreshColors();
    ready_ = true;
    return true;
}

void TileMap::ResetVisibility() {
    std::fill(visible_.begin(), visible_.end(), 0);
    std::fill(explored_.begin(), explored_.end(), 0);
    RefreshColors();
}

void TileMap::RefreshColors() {
    if (!ready_)
        return;
    for (unsigned y = 0; y < mapH_; ++y) {
        for (unsigned x = 0; x < mapW_; ++x) {
            const size_t idx = y * mapW_ + x;
            const size_t base = idx * 6;
            const uint8_t tile =
                (idx < tiles_.size()) ? tiles_[idx] : Protocol::TileType::Wall;
            const sf::Color col =
                DimColor(TileColor(tile), visible_[idx] != 0, explored_[idx] != 0);
            for (int i = 0; i < 6; ++i)
                vertices_[base + i].color = col;
        }
    }
}

bool TileMap::BlocksSight(uint8_t tile) const {
    return tile == Protocol::TileType::Wall || tile == Protocol::TileType::Door;
}

bool TileMap::HasLineOfSight(int x0, int y0, int x1, int y1) const {
    int dx = std::abs(x1 - x0);
    int dy = std::abs(y1 - y0);
    int sx = x0 < x1 ? 1 : -1;
    int sy = y0 < y1 ? 1 : -1;
    int err = dx - dy;
    int x = x0;
    int y = y0;

    while (true) {
        if (x == x1 && y == y1)
            return true;
        if (!(x == x0 && y == y0)) {
            if (x < 0 || y < 0 || x >= static_cast<int>(mapW_) || y >= static_cast<int>(mapH_))
                return false;
            const uint8_t tile = tiles_[static_cast<size_t>(y * mapW_ + x)];
            if (BlocksSight(tile))
                return false;
        }

        const int e2 = 2 * err;
        if (e2 > -dy) {
            err -= dy;
            x += sx;
        }
        if (e2 < dx) {
            err += dx;
            y += sy;
        }
    }
}

void TileMap::UpdateVisibility(float originX, float originY, int viewRadiusTiles) {
    if (!ready_ || tiles_.empty())
        return;

    const int ox = static_cast<int>(originX / tileSize_);
    const int oy = static_cast<int>(originY / tileSize_);
    std::fill(visible_.begin(), visible_.end(), 0);

    const int r = viewRadiusTiles;
    for (int ty = oy - r; ty <= oy + r; ++ty) {
        for (int tx = ox - r; tx <= ox + r; ++tx) {
            if (tx < 0 || ty < 0 || tx >= static_cast<int>(mapW_) || ty >= static_cast<int>(mapH_))
                continue;
            const int ddx = tx - ox;
            const int ddy = ty - oy;
            if (ddx * ddx + ddy * ddy > r * r)
                continue;
            if (HasLineOfSight(ox, oy, tx, ty)) {
                const size_t idx = static_cast<size_t>(ty * mapW_ + tx);
                visible_[idx] = 1;
                explored_[idx] = 1;
            }
        }
    }
    RefreshColors();
}

bool TileMap::IsVisible(unsigned tx, unsigned ty) const {
    if (tx >= mapW_ || ty >= mapH_)
        return false;
    return visible_[ty * mapW_ + tx] != 0;
}

bool TileMap::IsVisibleWorld(float wx, float wy) const {
    const unsigned tx = static_cast<unsigned>(std::max(0.f, wx / tileSize_));
    const unsigned ty = static_cast<unsigned>(std::max(0.f, wy / tileSize_));
    return IsVisible(tx, ty);
}

void TileMap::Draw(sf::RenderWindow& target) const {
    if (!ready_)
        return;
    target.draw(vertices_);
}

void TileMap::DrawExtractionZones(sf::RenderWindow& target) const {
    if (!ready_ || extractionCenters_.empty())
        return;

    constexpr float kRadius = 56.f;
    sf::CircleShape zone(kRadius);
    zone.setOrigin({kRadius, kRadius});
    zone.setFillColor({30, 180, 220, 35});
    zone.setOutlineColor({80, 220, 255, 140});
    zone.setOutlineThickness(2.f);

    for (const auto& center : extractionCenters_) {
        if (!IsVisibleWorld(center.x, center.y))
            continue;
        zone.setPosition(center);
        target.draw(zone);
    }
}

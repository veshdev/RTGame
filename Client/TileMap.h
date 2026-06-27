#pragma once

#include <SFML/Graphics.hpp>
#include <cstdint>
#include <vector>

class TileMap {
public:
    bool Build(const std::vector<uint8_t>& tiles, unsigned mapW, unsigned mapH, unsigned tileSize);
    void UpdateVisibility(float originX, float originY, int viewRadiusTiles);
    void ResetVisibility();
    void Draw(sf::RenderWindow& target) const;
    void DrawExtractionZones(sf::RenderWindow& target) const;
    bool Ready() const { return ready_; }
    bool IsVisible(unsigned tx, unsigned ty) const;
    bool IsVisibleWorld(float wx, float wy) const;

private:
    bool BlocksSight(uint8_t tile) const;
    bool HasLineOfSight(int x0, int y0, int x1, int y1) const;
    void RefreshColors();

    sf::VertexArray vertices_{sf::PrimitiveType::Triangles};
    std::vector<uint8_t> tiles_;
    std::vector<uint8_t> visible_;
    std::vector<uint8_t> explored_;
    std::vector<sf::Vector2f> extractionCenters_;
    unsigned mapW_ = 0;
    unsigned mapH_ = 0;
    unsigned tileSize_ = 32;
    bool ready_ = false;
};

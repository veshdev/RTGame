#pragma once



#include "Protocol.h"

#include <SFML/Graphics.hpp>



class Camera {

public:

    void SetViewport(sf::Vector2u size);

    void Follow(float worldX, float worldY);

    void Apply(sf::RenderWindow& window) const;

    void ResetUi(sf::RenderWindow& window) const;



    sf::Vector2f Center() const { return center_; }

    sf::Vector2f ScreenToWorld(sf::Vector2i screen) const;



private:

    sf::Vector2u viewport_{1280, 720};

    sf::Vector2f center_{};

};


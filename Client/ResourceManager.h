#pragma once

#include <SFML/Audio.hpp>
#include <SFML/Graphics.hpp>
#include <filesystem>
#include <memory>
#include <string>
#include <unordered_map>

class ResourceManager {
public:
    sf::Font* DefaultFont();
    std::shared_ptr<sf::Texture> Texture(const std::string& path);
    std::shared_ptr<sf::SoundBuffer> Sound(const std::string& path);

private:
    bool LoadDefaultFont();
    std::unique_ptr<sf::Font> defaultFont_;
    std::unordered_map<std::string, std::shared_ptr<sf::Texture>> textures_;
    std::unordered_map<std::string, std::shared_ptr<sf::SoundBuffer>> sounds_;
};

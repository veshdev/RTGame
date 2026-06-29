#include "ResourceManager.h"
#include <iostream>

std::shared_ptr<sf::Texture> ResourceManager::Texture(const std::string& path) {
    if (auto it = textures_.find(path); it != textures_.end()) {
        return it->second;
    }
    auto tex = std::make_shared<sf::Texture>();
    if (!tex->loadFromFile(std::filesystem::path(path))) {
        return nullptr;
    }
    textures_[path] = tex;
    return tex;
}

std::shared_ptr<sf::SoundBuffer> ResourceManager::Sound(const std::string& path) {
    if (auto it = sounds_.find(path); it != sounds_.end()) {
        return it->second;
    }
    auto buf = std::make_shared<sf::SoundBuffer>();
    if (!buf->loadFromFile(std::filesystem::path(path))) {
        return nullptr;
    }
    sounds_[path] = buf;
    return buf;
}

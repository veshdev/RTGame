#include "ResourceManager.h"
#include <iostream>

sf::Font* ResourceManager::DefaultFont() {
    if (!defaultFont_) {
        LoadDefaultFont();
    }
    return defaultFont_ ? defaultFont_.get() : nullptr;
}

bool ResourceManager::LoadDefaultFont() {
    defaultFont_ = std::make_unique<sf::Font>();
#ifdef _WIN32
    char* windir = nullptr;
    size_t len = 0;
    _dupenv_s(&windir, &len, "WINDIR");
    if (windir) {
        const std::filesystem::path candidates[] = {
            std::filesystem::path(windir) / "Fonts" / "segoeui.ttf",
            std::filesystem::path(windir) / "Fonts" / "arial.ttf",
        };
        free(windir);
        for (const auto& path : candidates) {
            if (std::filesystem::exists(path) && defaultFont_->openFromFile(path)) {
                return true;
            }
        }
    }
#endif
    const std::filesystem::path fallbacks[] = {
        "assets/font.ttf",
        "../assets/font.ttf",
    };
    for (const auto& path : fallbacks) {
        if (std::filesystem::exists(path) && defaultFont_->openFromFile(path)) {
            return true;
        }
    }
    std::cerr << "ResourceManager: no font found\n";
    defaultFont_.reset();
    return false;
}

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

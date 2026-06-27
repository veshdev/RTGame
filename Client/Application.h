#pragma once

#include "Protocol.h"
#include <cstdint>
#include <string>

class Application {
public:
    Application();
    ~Application();
    void Run();

private:
    bool vsync_ = true;
    float interpAlpha_ = 1.f;
    int hotbarSlot_ = static_cast<int>(Protocol::HotbarSlot::Pistol);
    bool wasAlive_ = false;
    bool predictionInit_ = false;
    float predictedX_ = 0.f;
    float predictedY_ = 0.f;
    float predictedAngle_ = 0.f;
};

#pragma once

#include <cstdint>
#include <cstring>
#include <span>
#include <vector>

class ByteReader {
public:
    explicit ByteReader(std::span<const uint8_t> data) : data_(data) {}

    bool remaining(size_t n) const { return offset_ + n <= data_.size(); }
    size_t offset() const { return offset_; }

    uint8_t u8() {
        return data_[offset_++];
    }

    uint16_t u16le() {
        const uint16_t v = static_cast<uint16_t>(data_[offset_]) |
                           (static_cast<uint16_t>(data_[offset_ + 1]) << 8);
        offset_ += 2;
        return v;
    }

    int16_t i16le() {
        return static_cast<int16_t>(u16le());
    }

    uint32_t u32le() {
        const uint32_t v = static_cast<uint32_t>(data_[offset_]) |
                           (static_cast<uint32_t>(data_[offset_ + 1]) << 8) |
                           (static_cast<uint32_t>(data_[offset_ + 2]) << 16) |
                           (static_cast<uint32_t>(data_[offset_ + 3]) << 24);
        offset_ += 4;
        return v;
    }

    float f32le() {
        float v{};
        std::memcpy(&v, data_.data() + offset_, 4);
        offset_ += 4;
        return v;
    }

    std::string stringPrefixed() {
        const uint8_t len = u8();
        if (!remaining(len)) return {};
        std::string s(reinterpret_cast<const char*>(data_.data() + offset_), len);
        offset_ += len;
        return s;
    }

private:
    std::span<const uint8_t> data_;
    size_t offset_ = 0;
};

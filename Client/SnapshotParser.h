#pragma once

#include "Entities.h"
#include <cstddef>
#include <cstdint>
#include <memory>

namespace SnapshotParser {

std::shared_ptr<Snapshot> Parse(const uint8_t* data, size_t len);

}

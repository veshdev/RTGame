#include "AccountStorage.h"

#include "Protocol.h"

#include <algorithm>
#include <fstream>
#include <sstream>

namespace {

bool ValidUsername(const std::string& s) {
    return !s.empty() && s.size() <= Protocol::UsernameMax;
}

bool ValidPassword(const std::string& s) {
    return !s.empty() && s.size() <= Protocol::PasswordMax;
}

std::string EscapeField(std::string s) {
    for (char& c : s) {
        if (c == '\t' || c == '\r' || c == '\n')
            c = ' ';
    }
    return s;
}

} // namespace

AccountStorage& AccountStorage::Instance() {
    static AccountStorage storage;
    return storage;
}

AccountStorage::AccountStorage() {
    Load();
}

void AccountStorage::Select(int index) {
    if (accounts_.empty()) {
        selected_ = 0;
        return;
    }
    selected_ = std::clamp(index, 0, static_cast<int>(accounts_.size()) - 1);
}

const SavedAccount* AccountStorage::SelectedAccount() const {
    if (accounts_.empty() || selected_ < 0 || selected_ >= static_cast<int>(accounts_.size()))
        return nullptr;
    return &accounts_[static_cast<size_t>(selected_)];
}

void AccountStorage::Remember(const std::string& username, const std::string& password) {
    if (!ValidUsername(username) || !ValidPassword(password))
        return;

    SavedAccount entry{username, password};
    accounts_.erase(std::remove_if(accounts_.begin(), accounts_.end(),
                                   [&](const SavedAccount& a) { return a.username == username; }),
                    accounts_.end());
    accounts_.insert(accounts_.begin(), std::move(entry));
    if (accounts_.size() > 8)
        accounts_.resize(8);
    selected_ = 0;
    Save();
}

void AccountStorage::Load() {
    accounts_.clear();
    selected_ = 0;

    std::ifstream in(path_);
    if (!in)
        return;

    std::string line;
    while (std::getline(in, line)) {
        if (line.empty())
            continue;

        SavedAccount entry;
        const size_t tab = line.find('\t');
        if (tab == std::string::npos) {
            entry.username = EscapeField(line);
            if (!ValidUsername(entry.username))
                continue;
        } else {
            entry.username = EscapeField(line.substr(0, tab));
            entry.password = EscapeField(line.substr(tab + 1));
            if (!ValidUsername(entry.username) || !ValidPassword(entry.password))
                continue;
        }
        accounts_.push_back(std::move(entry));
    }
}

void AccountStorage::Save() {
    std::ofstream out(path_);
    if (!out)
        return;
    for (const auto& account : accounts_) {
        out << EscapeField(account.username) << '\t' << EscapeField(account.password) << '\n';
    }
}

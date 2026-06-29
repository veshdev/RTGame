#pragma once

#include <string>
#include <vector>

struct SavedAccount {
    std::string username;
};

class AccountStorage {
public:
    static AccountStorage& Instance();

    const std::vector<SavedAccount>& Accounts() const { return accounts_; }
    int SelectedIndex() const { return selected_; }
    void Select(int index);
    void Remember(const std::string& username);
    const SavedAccount* SelectedAccount() const;

private:
    AccountStorage();
    void Load();
    void Save();

    std::vector<SavedAccount> accounts_;
    int selected_ = 0;
    std::string path_ = "recent_accounts.txt";
};

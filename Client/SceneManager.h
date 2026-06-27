#pragma once



enum class Scene {

    MainMenu,

    Login,

    Register,

    RoomList,

    CreateRoom,

    JoinRoom,

    Lobby,

    Loading,

    Playing,

    Pause,

    Settings

};



class SceneManager {

public:

    Scene Current() const { return current_; }

    Scene Previous() const { return previous_; }



    void GoTo(Scene scene) {

        previous_ = current_;

        current_ = scene;

    }



    bool IsGameplay() const {

        return current_ == Scene::Playing || current_ == Scene::Pause;

    }



private:

    Scene current_ = Scene::MainMenu;

    Scene previous_ = Scene::MainMenu;

};


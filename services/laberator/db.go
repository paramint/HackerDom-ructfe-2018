package main

import (
	"fmt"
	"github.com/jinzhu/gorm"
	_ "github.com/jinzhu/gorm/dialects/sqlite"
)

type User struct {
	gorm.Model
	Login string
	Password string
}

type DBApi struct {
	db *gorm.DB
}

type DBApiError string

func (err DBApiError) Error() string {
	return fmt.Sprintf("Database api error: %v", string(err))
}

func (api *DBApi) IsUserExist(login *string) bool {
	var users []User
	api.db.Where("Login = ?", *login).Find(&users)
	return len(users) != 0
}

func (api *DBApi) Register(login, password *string) error {
	var users []User
	api.db.Where("Login = ?", login).Find(&users)
	if len(users) != 0 {
		return DBApiError(fmt.Sprintf("User with login '%s' is already exist", *login))
	}
	user := User{Login: *login, Password: *password}
	api.db.Create(&user)
	return nil
}

func (api *DBApi) Validate(login, password *string) bool {
	var users []User
	api.db.Where("Login = ?", login).Find(&users)
	if len(users) != 1 {
		return false
	}
	if users[0].Password == *password {
		return true
	}
	return false
}

func (api *DBApi) Init() {
	var err error
	api.db, err = gorm.Open("sqlite3", "test.db")
	if err != nil {
		panic("failed to connect database")
	}
	api.db.AutoMigrate(&User{})
}
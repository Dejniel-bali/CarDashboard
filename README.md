# Car Simulator and Dashboard

## Description
This is a real-time car monitoring system. The project simulates a car, saves the travel data to a database, and shows the information on a web dashboard. 

## About the Project
This project has three main parts that work together:

* **Simulator (C#):** This part acts like the car. It creates data like speed, engine temperature, and fuel level. It also checks the server for new commands.
* **Server & Database:** This is the middle part. It receives data from the car and saves it using Entity Framework and an SQLite database. It holds the data for the website to read.
* **Web Dashboard (HTML / JavaScript):** This is the screen for the user or admin. It shows the car data in real-time. The dashboard has warning lights (for example, it turns red if the temperature is too high). The admin can also use it to send commands to the car, like simulating a flat tire.

## Key Features
* **Real-time Monitoring:** See speed, fuel, and temperature update live.
* **Warning System:** JavaScript logic changes the colors of dashboard lights if the car has a problem (like low oil).
* **Admin Commands:** The user can send events to the car from the web.
* **Time Control:** You can speed up the simulation interval to test data generation faster.
* **Data Storage:** All driving data is safely stored in an SQLite database.

## Technologies Used
* **Simulator:** C#
* **Backend / Server:** C#, Entity Framework
* **Database:** SQLite
* **Frontend:** HTML, JavaScript

## How It Works (Architecture)
1. The **Simulator** generates data and sends it to the **Server**.
2. The **Server** saves the data into the **SQLite database**.
3. The **Web Dashboard** asks the **Server** for the latest data and draws it on the screen.
4. If an admin clicks a command on the dashboard, it goes to the **Server**.
5. The **Simulator** frequently asks the **Server** if there are new commands and reacts to them (for example, slowing down if there is a flat tire).

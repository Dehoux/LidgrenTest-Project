﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Lidgren.Network;

public class Lobby {

    private List<Player> lobbyPlayers;
    private List<Room> rooms; 

    public Lobby()
    {
        rooms = new List<Room>();
    }

    public List<Room> GetRooms()
    {
        return rooms;
    }

    public void AddPlayer(NetIncomingMessage netIncomingMessage)
    {

    }

    public void AddRoom(Room room)
    {
        rooms.Add(room);
    }

    public Player FindPlayer(int playerId)
    {
        return lobbyPlayers.Find(x => x.Id == playerId);
    }
}
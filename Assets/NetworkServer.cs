using System;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine.UI;

public class NetworkServer : MonoBehaviour
{
    public Text serverStatus;
    public Text numClients;
    public Text clientMessagesLabel;
    public UdpNetworkDriver driver;

    private NativeList<NetworkConnection> connections;
    private NativeList<float> clientTimers;
    private List<string> clientMessages;
    private List<Vector2> clientPositions;
    private float dropTime;
    private float moveSpeed;

    void Start ()
    {
        driver = new UdpNetworkDriver(new INetworkParameter[0]);
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = 54321;
        if (driver.Bind(endpoint) != 0)
            serverStatus.text = "Server Status: Port Failure";
        else
        {
             driver.Listen();
            serverStatus.text = "Server Status: Online";
        }
        connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
        clientTimers = new NativeList<float>(16, Allocator.Persistent);
        clientMessages = new List<string>();
        clientPositions = new List<Vector2>();
        clientMessagesLabel.text = "";
        dropTime = 2.0f;
        moveSpeed = 10f;
    }

    public void OnDestroy()
    {
        driver.Dispose();
        connections.Dispose();
        clientTimers.Dispose();
    }

    void Update ()
    {
        driver.ScheduleUpdate().Complete();
        AcceptNewConnections();
        numClients.text = "Clients: " + connections.Length.ToString();
        PrintClientMessages();
        UpdateClientPositions(Time.deltaTime);

        DataStreamReader stream;
        for (int i = 0; i < connections.Length; i++)
        {
            if (!connections[i].IsCreated)
                Assert.IsTrue(true);

            clientTimers[i] += Time.deltaTime;
            if (clientTimers[i] > dropTime)
            {
                DropConnection(i);
            }
            else
            {
                NetworkEvent.Type cmd;
                while ((cmd = driver.PopEventForConnection(connections[i], out stream)) != NetworkEvent.Type.Empty)
                {
                    clientMessages[i] = ReadMessage(stream);
                    clientTimers[i] = 0;
                    SendMessage(GenerateFinalString(), driver, connections[i]);
                }
            }
        }
    }

    private void AcceptNewConnections()
    {
        NetworkConnection c;
        while ((c = driver.Accept()) != default(NetworkConnection))
        {
            connections.Add(c);
            clientTimers.Add(0);
            clientMessages.Add("0-0-0-0");
            clientPositions.Add(Vector2.zero);
            Debug.Log("New client connected.");
        }
    }

    private void DropConnection(int i)
    {
        Debug.Log("Client " + i + " disconnected.");
        connections[i].Disconnect(driver);
        connections[i] = default(NetworkConnection);
        connections.RemoveAtSwapBack(i);
        clientTimers.RemoveAtSwapBack(i);
        clientMessages.RemoveAtSwapBack(i);
        clientPositions.RemoveAtSwapBack(i);
    }

    private void PrintClientMessages()
    {
        string result = "";
        foreach (string str in clientMessages)
        {
            result += str + "\n";
        }
        clientMessagesLabel.text = result;
    }

    private void UpdateClientPositions(float dt)
    {
        for (int i = 0; i < clientMessages.Count; i++)
        {
            string[] inputs = clientMessages[i].Split('-');
            float newX = clientPositions[i].x;
            float newY = clientPositions[i].y;
            if (inputs[0] == "1")
            { // W Pressed
                newY += (moveSpeed * dt);
            }
            if (inputs[1] == "1")
            { // S Pressed
                newY -= (moveSpeed * dt);
            }
            if (inputs[2] == "1")
            { // A Pressed
                newX -= (moveSpeed * dt);
            }
            if (inputs[3] == "1")
            { // D Pressed
                newX += (moveSpeed * dt);
            }
            clientPositions[i] = new Vector2(newX, newY);
        }
    }

    private string GenerateFinalString()
    {
        string result = "";
        foreach (Vector2 pos in clientPositions)
        {
            result += pos.x.ToString() + "e" + pos.y.ToString() + "a";
        }
        result = result.Remove(result.Length - 1, 1);
        Debug.Log(result);
        return result;
    }

    private void SendMessage(string data, UdpNetworkDriver driver, NetworkConnection conn)
    {
        Byte[] sendBytes = Encoding.ASCII.GetBytes(data);
        using (var writer = new DataStreamWriter(1024, Allocator.Temp))
        {
            writer.Write(sendBytes, sendBytes.Length);
            conn.Send(driver, writer);
        }
    }

    private string ReadMessage(DataStreamReader stream)
    {
        var readerCtx = default(DataStreamReader.Context);
        var infoBuffer = new byte[stream.Length];
        stream.ReadBytesIntoArray(ref readerCtx, ref infoBuffer, stream.Length);
        return Encoding.ASCII.GetString(infoBuffer);
    }
}
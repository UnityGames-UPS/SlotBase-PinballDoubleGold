using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;
using Best.SocketIO;
using Best.SocketIO.Events;
using DG.Tweening;

public class SocketIOManager : MonoBehaviour
{
  [SerializeField] private SlotBehaviour slotManager;
  [SerializeField] private UIManager uiManager;
  [SerializeField] internal JSFunctCalls JSManager;
  [SerializeField] private string testToken;
  [SerializeField] private GameObject RaycastBlocker;
  internal GameData InitialData = null;
  internal UiData UIData = null;
  internal Features GameFeatures = null;   // top-level "features" config from game:init
  internal Root ResultData = null;
  internal Player PlayerData = null;
  internal bool isResultdone = false;
  internal bool SetInit = false;

  private SocketManager manager;
  protected string SocketURI = null;
  // protected string TestSocketURI = "https://game-crm-rtp-backend.onrender.com/";
  protected string TestSocketURI = "https://devrealtime.dingdinghouse.com";
  protected string nameSpace = "playground";
  private Socket gameSocket;
  protected string gameID = "SL-TXT";
  //protected string gameID = "";
  private const int maxReconnectionAttempts = 6;
  private readonly TimeSpan reconnectionDelay = TimeSpan.FromSeconds(10);
  string myAuth = null;

  private bool isConnected = false; //Back2 Start
  private bool hasEverConnected = false;
  private const int MaxReconnectAttempts = 5;
  private const float ReconnectDelaySeconds = 2f;

  private float lastPongTime = 0f;
  private float pingInterval = 2f;
  private float pongTimeout = 3f;
  private bool waitingForPong = false;
  private int missedPongs = 0;
  private const int MaxMissedPongs = 5;
  private Coroutine PingRoutine; //Back2 end
  private void Awake()
  {
    //Debug.unityLogger.logEnabled = false;
    SetInit = false;
  }

  private void Start()
  {
    OpenSocket();
  }

  void CloseGame()
  {
    Debug.Log("Unity: Closing Game");
    StartCoroutine(CloseSocket());  
  }

  void ReceiveAuthToken(string jsonData)
  {
    Debug.Log("Received data: " + jsonData);
    var data = JsonUtility.FromJson<AuthTokenData>(jsonData);
    SocketURI = data.socketURL;
    myAuth = data.cookie;
    nameSpace = data.nameSpace;
  }

  private void OpenSocket()
  {
    //Create and setup SocketOptions
    SocketOptions options = new SocketOptions(); //Back2 Start
    options.AutoConnect = false;
    options.Reconnection = false;
    options.Timeout = TimeSpan.FromSeconds(3); //Back2 end
    options.ConnectWith = Best.SocketIO.Transports.TransportTypes.WebSocket;

#if UNITY_WEBGL && !UNITY_EDITOR
        JSManager.SendCustomMessage("authToken");
        StartCoroutine(WaitForAuthToken(options));
#else
    object authFunction(SocketManager manager, Socket socket)
    {
      return new
      {
        token = testToken
      };
    }
    options.Auth = authFunction;
    SetupSocketManager(options);
#endif
  }

  private IEnumerator WaitForAuthToken(SocketOptions options)
  {
    // Wait until myAuth is not null
    while (myAuth == null)
    {
      Debug.Log("My Auth is null");
      yield return null;
    }
    while (SocketURI == null)
    {
      Debug.Log("My Socket is null");
      yield return null;
    }
    Debug.Log("My Auth is not null");
    // Once myAuth is set, configure the authFunction
    object authFunction(SocketManager manager, Socket socket)
    {
      return new
      {
        token = myAuth
      };
    }
    options.Auth = authFunction;
    Debug.Log("Auth function configured with token: " + myAuth);

    // Proceed with connecting to the server
    SetupSocketManager(options);

    yield return null;
  }

  private void SetupSocketManager(SocketOptions options)
  {
    Debug.Log("Setup socket manager");
    // Create and setup SocketManager
#if UNITY_EDITOR
    this.manager = new SocketManager(new Uri(TestSocketURI), options);
#else
    this.manager = new SocketManager(new Uri(SocketURI), options);
#endif

    if (string.IsNullOrEmpty(nameSpace))
    {
      gameSocket = this.manager.Socket;
    }
    else
    {
      Debug.Log("nameSpace: " + nameSpace);
      gameSocket = this.manager.GetSocket("/" + nameSpace);
    }
    // Set subscriptions
    gameSocket.On<ConnectResponse>(SocketIOEventTypes.Connect, OnConnected);
    gameSocket.On(SocketIOEventTypes.Disconnect, OnDisconnected); //Back2 Start
    gameSocket.On<Error>(SocketIOEventTypes.Error, OnError);
    gameSocket.On<string>("game:init", OnListenEvent);
    gameSocket.On<string>("result", OnResult);
    gameSocket.On<bool>("socketState", OnSocketState);
    gameSocket.On<string>("internalError", OnSocketError);
    gameSocket.On<string>("alert", OnSocketAlert);
    gameSocket.On<string>("pong", OnPongReceived); //Back2 Start
    gameSocket.On<string>("AnotherDevice", OnSocketOtherDevice);

    manager.Open(); //Back2 Start
  }

  // Connected event handler implementation
  void OnConnected(ConnectResponse resp) //Back2 Start
  {
    Debug.Log("✅ Connected to server.");

    if (hasEverConnected)
    {
      uiManager.CheckAndClosePopups();
    }

    isConnected = true;
    hasEverConnected = true;
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
    SendPing();
  } //Back2 end

  private void OnDisconnected() //Back2 Start
  {
    Debug.LogWarning("⚠️ Disconnected from server.");
    isConnected = false;
    ResetPingRoutine();
    uiManager.DisconnectionPopup();
  } //Back2 end

  private void OnPongReceived(string data) //Back2 Start
  {
    // Debug.Log("✅ Received pong from server.");
    waitingForPong = false;
    missedPongs = 0;
    lastPongTime = Time.time;
    // Debug.Log($"⏱️ Updated last pong time: {lastPongTime}");
    // Debug.Log($"📦 Pong payload: {data}");
  } //Back2 end

private void OnError(Error err)
  {
    Debug.LogError("Socket Error Message: " + err);
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("error");
#endif
  }

  void OnResult(string data)
  {
    ParseResponse(data);
  }

  private void OnListenEvent(string data)
  {
    ParseResponse(data);
  }

  private void OnSocketState(bool state)
  {
    if (state)
    {
      Debug.Log("my state is " + state);
    }
  }
  private void OnSocketError(string data)
  {
    Debug.Log("Received error with data: " + data);
  }

  private void OnSocketAlert(string data)
  {
    Debug.Log("Received alert with data: " + data);
  }

  private void OnSocketOtherDevice(string data)
  {
    Debug.Log("Received Device Error with data: " + data);
    uiManager.ADfunction();
  }

  private void SendPing() //Back2 Start
  {
    ResetPingRoutine();
    PingRoutine = StartCoroutine(PingCheck());
  }

  void ResetPingRoutine()
  {
    if (PingRoutine != null)
    {
      StopCoroutine(PingRoutine);
    }
    PingRoutine = null;
  }

  private IEnumerator PingCheck()
  {
    while (true)
    {
      // Debug.Log($"🟡 PingCheck | waitingForPong: {waitingForPong}, missedPongs: {missedPongs}, timeSinceLastPong: {Time.time - lastPongTime}");

      if (missedPongs == 0)
      {
        uiManager.CheckAndClosePopups();
      }

      // If waiting for pong, and timeout passed
      if (waitingForPong)
      {
        missedPongs++;
        // Debug.LogWarning($"⚠️ Pong missed #{missedPongs}/{MaxMissedPongs}");

        if (missedPongs == 2)
        {
          uiManager.ReconnectionPopup(missedPongs, MaxMissedPongs);
        }
        else
        {
          uiManager.UpdateReconnectingAttempt(missedPongs, MaxMissedPongs);
        }

        if (missedPongs >= MaxMissedPongs)
        {
          // Debug.LogError("❌ Unable to connect to server — 5 consecutive pongs missed.");
          isConnected = false;
          uiManager.DisconnectionPopup();
          yield break;
        }
      }

      // Send next ping
      waitingForPong = true;
      lastPongTime = Time.time;
      // Debug.Log("📤 Sending ping...");
      SendDataWithNamespace("ping");
      yield return new WaitForSeconds(pingInterval);
    }
  } //Back2 end
  internal void SendDataWithNamespace(string eventName, string json = null)
  {
    // Send the message
    if (gameSocket != null && gameSocket.IsOpen)
    {
      if (json != null)
      {
        gameSocket.Emit(eventName, json);
        Debug.Log("JSON data sent: " + json);
      }
      else
      {
        gameSocket.Emit(eventName);
      }
    }
    else
    {
      Debug.LogWarning("Socket is not connected.");
    }
  }

  internal IEnumerator CloseSocket() //Back2 Start
  {
    RaycastBlocker.SetActive(true);
    ResetPingRoutine();

    Debug.Log("Closing Socket");

    manager?.Close();
    manager = null;

    Debug.Log("Waiting for socket to close");

    yield return new WaitForSeconds(0.5f);

    Debug.Log("Socket Closed");

#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnExit"); //Telling the react platform user wants to quit and go back to homepage
#endif
  } //Back2 end

  private void ParseResponse(string jsonObject)
  {
    Debug.Log(jsonObject);
    Root myData = JsonConvert.DeserializeObject<Root>(jsonObject);

    string id = myData.id;

    switch (id)
    {
      case "initData":
        {
          InitialData = myData.gameData;
          UIData = myData.uiData;
          GameFeatures = myData.features;
          PlayerData = myData.player;

          if (!SetInit)
          {
            List<string> LinesString = ConvertListListIntToListString(InitialData.lines);
            PopulateSlotSocket(LinesString);
            SetInit = true;
          }
          else
          {
            RefreshUI();
          }
          break;
        }
      case "ResultData":
        {
          ResultData = myData;
          PlayerData = myData.player;
          isResultdone = true;
          break;
        }
    }
  }

  private void RefreshUI()
  {
    uiManager.InitialiseUI(InitialData.bets, UIData.paylines.symbols);
  }

  private void PopulateSlotSocket(List<string> LineIds)
  {
    // slotManager.shuffleInitialMatrix();
    slotManager.InitializeMatrix();
    for (int i = 0; i < LineIds.Count; i++)
    {
      slotManager.FetchLines(LineIds[i], i);
    }
    slotManager.SetInitialUI();
#if UNITY_WEBGL && !UNITY_EDITOR
    JSManager.SendCustomMessage("OnEnter");
#endif
    RaycastBlocker.SetActive(false);
  }

  internal void AccumulateResult(int currBet)
  {
    isResultdone = false;
    MessageData message = new();
    message.type = "SPIN";
    message.payload.betIndex = currBet;

    // Serialize message data to JSON
    string json = JsonUtility.ToJson(message);
    SendDataWithNamespace("request", json);
  }

  // Fires a single pinball-bonus shot. Identical request shape to a spin, but type "BONUS" —
  // the backend replies on the same "result"/"ResultData" channel with the bonus payload
  // (bonusWin/isOver/selectedIndex/isSpecial/bonusState) instead of the spin payload.
  internal void AccumulateBonusResult(int currBet)
  {
    isResultdone = false;
    MessageData message = new();
    message.type = "BONUS";
    message.payload.betIndex = currBet;

    string json = JsonUtility.ToJson(message);
    SendDataWithNamespace("request", json);
  }

  private List<string> ConvertListListIntToListString(List<List<int>> listOfLists)
  {
    List<string> resultList = new List<string>();

    foreach (List<int> innerList in listOfLists)
    {
      // Convert each integer in the inner list to string
      List<string> stringList = new List<string>();
      foreach (int number in innerList)
      {
        stringList.Add(number.ToString());
      }

      // Join the string representation of integers with ","
      string joinedString = string.Join(",", stringList.ToArray()).Trim();
      resultList.Add(joinedString);
    }

    return resultList;
  }
}

[Serializable]
public class MessageData
{
  public string type;
  public Data payload = new();
}

[Serializable]
public class Data
{
  public int betIndex;
  public string Event;
  public List<int> index;
  public int option;
}

[Serializable]
public class GameData
{
  public List<List<int>> lines { get; set; }
  public List<double> bets { get; set; }
  public int totalLines { get; set; }
}

[Serializable]
public class Root
{
  public bool success { get; set; }
  public Payload payload { get; set; }
  public Features features { get; set; } = new Features();
  //Initial Data
  public string id { get; set; }
  public GameData gameData { get; set; }
  public UiData uiData { get; set; }
  public Player player { get; set; }
}

// Root.features. On a SL-PDG game:init this carries the top-level game config (baseCoinValue,
// pinball prizes, doubleSymbol, anyPayouts, linePayout) — captured into SocketManager.GameFeatures.
// Result payloads have no top-level "features" (the per-spin pinball flag lives at payload.features),
// so on results this whole object stays default.
[Serializable]
public class Features
{
  // SL-PDG init config (populated only from the game:init "features" block; null on results).
  public double baseCoinValue { get; set; }
  public PinballConfig pinball { get; set; }
  public DoubleSymbolConfig doubleSymbol { get; set; }
  public Dictionary<string, double> anyPayouts { get; set; }
  public List<LinePayout> linePayout { get; set; }
}

[Serializable]
public class PinballConfig
{
  public bool enabled { get; set; }
  public int jackpot { get; set; }                   // jackpot point value (also the last chutePrizes entry)
  public List<int> prizes { get; set; }              // UFO base point values, indexed by result selectedIndex
  public List<int> chutePrizes { get; set; }         // 5 marble values, top->bottom; last = jackpot
  public List<SpecialPrize> specialPrizes { get; set; }
}

[Serializable]
public class SpecialPrize
{
  public int prize { get; set; }
  public int extraShots { get; set; }
}

[Serializable]
public class DoubleSymbolConfig
{
  public bool enabled { get; set; }
  public List<int> multipliers { get; set; }         // e.g. [2,4,8] — the tiers behind WinningLine.doubleMultiplier
}

[Serializable]
public class LinePayout
{
  public double payout { get; set; }
  public List<int> payline { get; set; }
  public List<int> symbols { get; set; }
}

[Serializable]
public class Payload
{
  // SL-PDG live result payload fields (SPIN):
  public List<List<string>> reels { get; set; }
  public List<WinningLine> winningLines { get; set; }
  public double totalWin { get; set; }
  public ResultFeatures features { get; set; } = new ResultFeatures();

  // SL-PDG pinball-BONUS result fields. Arrive on the same envelope as a spin result; they stay
  // default on SPIN payloads and the SPIN fields stay default on BONUS payloads (Newtonsoft leaves
  // absent fields at their defaults). See pdg-backend-clarifications for the field semantics.
  public double bonusWin { get; set; }         // this shot's award (money)
  public bool isOver { get; set; }             // feature-complete flag (end signal)
  public int selectedIndex { get; set; }       // prize index: prizes[] (UFO), specialPrizes[] (special), or chutePrizes[] (chute; == chuteHits-1)
  public bool isSpecial { get; set; }          // hit a +shot special UFO
  public bool isChute { get; set; }            // this shot went down the chute (marble) instead of a UFO
  public BonusState bonusState { get; set; } = new BonusState();
}

[Serializable]
public class BonusState
{
  public int shotsRemaining { get; set; }      // authoritative post-shot count (already nets extraShots)
  public double totalBonusWin { get; set; }    // running feature total; credited to balance on isOver
  public int extraShots { get; set; }          // extra shots granted by this shot (special pockets)
  public int chuteHits { get; set; }           // persistent marble-chute counter this bonus; chute shot lands on marbles[chuteHits-1]
}

[Serializable]
public class WinningLine
{
  public int lineIndex { get; set; }
  public double payout { get; set; }
  public List<List<int>> positions { get; set; }
  public double doubleMultiplier { get; set; }
  public int doubleCount { get; set; }
}

[Serializable]
public class ResultFeatures
{
  public PinballTriggerInfo pinball { get; set; } = new PinballTriggerInfo();
}

[Serializable]
public class PinballTriggerInfo
{
  public bool triggered { get; set; }
  public int shotsRemaining { get; set; }      // starting shot count, present on the triggering spin
}

[Serializable]
public class UiData
{
  public Paylines paylines { get; set; }
}

[Serializable]
public class Paylines
{
  public List<Symbol> symbols { get; set; }
}

[Serializable]
public class Symbol
{
  public int id { get; set; }
  public string name { get; set; }
  public double payout { get; set; }
  public string description { get; set; }
  public string group { get; set; }
}

[Serializable]
public class Player
{
  public double balance { get; set; }
}

[Serializable]
public class AuthTokenData
{
  public string cookie;
  public string socketURL;
  public string nameSpace;
}

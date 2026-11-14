using System.Collections;
using PokerShop;
using UnityEngine;

public class GameFlow : MonoBehaviour
{
    [SerializeField] LevelIntroScreen levelIntro;

    bool _introRunning;

    void Start()
    {
        if (levelIntro == null) { Debug.LogError("GameFlow: Level Intro is not assigned!"); return; }
        if (Shop.Service == null) { Debug.LogError("GameFlow: Shop.Service is null"); return; }

        levelIntro.gameObject.SetActive(false);
        GameData.CurrentLevel = 1;
        Shop.Service.ShopClosed += OnShopClosed;
        Debug.Log("GameFlow subscribed", this);
    }

    void OnDestroy()
    {
        if (Shop.Service != null)
            Shop.Service.ShopClosed -= OnShopClosed;
    }

    void Update()
    {
        if (Shop.Service != null && Shop.Service.IsOpen && Time.timeScale != 0f)
            Time.timeScale = 0f;

        // TEMP: press L to simulate finishing a level
        if (Input.GetKeyDown(KeyCode.L) && !Shop.Service.IsOpen)
            Shop.Open(new ShopContext { Round = GameData.CurrentLevel, Tag = "before" });
    }

    void OnShopClosed()
    {
        Debug.Log($"GameFlow ShopClosed. Level={GameData.CurrentLevel}, shop open={Shop.Service.IsOpen}, introRunning={_introRunning}", this);

        if (_introRunning) return;   // ignore duplicate triggers
        StartCoroutine(BeginLevel());
    }

    IEnumerator BeginLevel()
    {
        _introRunning = true;

        int level = GameData.CurrentLevel;
        GameData.CurrentLevel++;

        Time.timeScale = 0f;
        yield return levelIntro.Show(level);
        Time.timeScale = 1f;

        _introRunning = false;
    }
}
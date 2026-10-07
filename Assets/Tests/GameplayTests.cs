using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameplayTests : InputTestFixture
{
    GameManager gm;
    PlayerController player;
    UIManager ui;

    IEnumerator Load()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return SceneManager.LoadSceneAsync("CoinRush3D");
        yield return null;
        gm = GameManager.Instance;
        player = gm.player;
        ui = gm.ui;
    }

    [UnityTest]
    public IEnumerator StartScreenShowsAndTimerWaits()
    {
        yield return Load();
        Assert.AreEqual(GameState.Start, gm.State);
        Assert.IsTrue(ui.startScreen.activeSelf);
        Assert.AreEqual(14, gm.TotalCoins);
        Assert.AreEqual(4, Object.FindObjectsByType<EnemyController>().Length);
        yield return new WaitForSeconds(0.5f);
        Assert.AreEqual(60f, gm.TimeLeft, 0.001f, "Timer must not run before START GAME");
    }

    [UnityTest]
    public IEnumerator StartGameResetsAndRunsTimer()
    {
        yield return Load();
        ui.OnStartClicked();
        yield return new WaitForSeconds(1.1f);
        Assert.AreEqual(GameState.Playing, gm.State);
        Assert.IsFalse(ui.startScreen.activeSelf);
        Assert.AreEqual(0, gm.Score);
        Assert.AreEqual(3, gm.Health);
        Assert.Less(gm.TimeLeft, 59.2f);
        Assert.AreEqual("Time: 59", ui.timeText.text);
    }

    [UnityTest]
    public IEnumerator WKeyMovesPlayerForward()
    {
        Keyboard kb = InputSystem.AddDevice<Keyboard>();
        yield return Load();
        ui.OnStartClicked();
        Vector3 start = player.transform.position;
        Press(kb.wKey);
        yield return new WaitForSeconds(0.6f);
        Release(kb.wKey);
        Assert.Greater(player.transform.position.z - start.z, 1.5f, "W should walk forward toward the portal");
    }

    [UnityTest]
    public IEnumerator CollectingCoinAddsTenPoints()
    {
        yield return Load();
        ui.OnStartClicked();
        Coin coin = gm.coinsRoot.GetComponentsInChildren<Coin>().OrderBy(c => Vector3.Distance(c.transform.position, player.transform.position)).First();
        player.Teleport(coin.transform.position + Vector3.down * 0.9f + Vector3.back * 1.5f);
        yield return new WaitForFixedUpdate();
        player.Teleport(coin.transform.position + Vector3.down * 0.9f);
        for (int i = 0; i < 10 && coin; i++) yield return new WaitForFixedUpdate();
        yield return null;
        Assert.IsTrue(coin == null, "Coin should be removed");
        Assert.AreEqual(10, gm.Score);
        Assert.AreEqual(1, gm.CoinsCollected);
        Assert.AreEqual("Score: 10", ui.scoreText.text);
    }

    [UnityTest]
    public IEnumerator EnemyContactDamagesAndKnocksBack()
    {
        yield return Load();
        ui.OnStartClicked();
        EnemyController enemy = Object.FindObjectsByType<EnemyController>().First();
        Vector3 e = enemy.transform.position;
        player.Teleport(new Vector3(e.x + 0.6f, 0.05f, e.z));
        yield return null;
        yield return null;
        Assert.AreEqual(2, gm.Health);
        yield return new WaitForSeconds(0.3f);
        Assert.Greater(Vector3.Distance(player.transform.position, enemy.transform.position), 1.2f, "Player should be pushed away");
        Assert.AreEqual(2, gm.Health, "Invulnerability should prevent repeated damage");
    }

    [UnityTest]
    public IEnumerator ThreeHitsIsGameOver()
    {
        yield return Load();
        gm.invulnerableTime = 0f;
        ui.OnStartClicked();
        for (int i = 0; i < 3; i++)
        {
            gm.DamagePlayer(player.transform.position + Vector3.forward);
            yield return null;
        }
        Assert.AreEqual(GameState.GameOver, gm.State);
        yield return new WaitForSeconds(0.9f);
        Assert.IsTrue(ui.gameOverScreen.activeSelf);
        Assert.AreEqual("GAME OVER", ui.gameOverTitle.text);
    }

    [UnityTest]
    public IEnumerator TimerReachingZeroIsTimeUp()
    {
        yield return Load();
        gm.levelTime = 1f;
        ui.OnStartClicked();
        yield return new WaitForSeconds(1.3f);
        Assert.AreEqual(GameState.TimeUp, gm.State);
        yield return new WaitForSeconds(0.8f);
        Assert.IsTrue(ui.gameOverScreen.activeSelf);
        Assert.AreEqual("TIME UP!", ui.gameOverTitle.text);
    }

    [UnityTest]
    public IEnumerator EnteringPortalWins()
    {
        yield return Load();
        ui.OnStartClicked();
        Portal portal = Object.FindAnyObjectByType<Portal>();
        player.Teleport(portal.transform.position + Vector3.back * 2.5f);
        yield return new WaitForFixedUpdate();
        player.Teleport(portal.transform.position + Vector3.up * 0.05f);
        for (int i = 0; i < 10 && gm.State == GameState.Playing; i++) yield return new WaitForFixedUpdate();
        Assert.AreEqual(GameState.Won, gm.State);
        yield return new WaitForSeconds(1.3f);
        Assert.IsTrue(ui.winScreen.activeSelf);
        StringAssert.Contains("Final Score", ui.winScore.text);
    }

    [UnityTest]
    public IEnumerator RestartStartsFreshGame()
    {
        yield return Load();
        gm.invulnerableTime = 0f;
        ui.OnStartClicked();
        gm.DamagePlayer(player.transform.position + Vector3.forward);
        ui.OnRestartClicked();
        yield return null;
        yield return null;
        GameManager fresh = GameManager.Instance;
        Assert.AreNotSame(gm, fresh);
        Assert.AreEqual(GameState.Playing, fresh.State, "Restart should skip the start screen");
        Assert.AreEqual(3, fresh.Health);
        Assert.AreEqual(0, fresh.Score);
    }
}

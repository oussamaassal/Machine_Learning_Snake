using UnityEngine;

public class GUI_SnakeAgent : MonoBehaviour
{
    [SerializeField] private SnakeAgent _snakeAgent;

    private GUIStyle _defualtStyle = new GUIStyle();
    private GUIStyle _positiveStyle = new GUIStyle();
    private GUIStyle _negativeStyle = new GUIStyle();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _defualtStyle.fontSize = 20;
        _defualtStyle.normal.textColor = Color.yellow;

        _positiveStyle.fontSize = 20;
        _positiveStyle.normal.textColor = Color.green;

        _negativeStyle.fontSize = 20;
        _negativeStyle.normal.textColor = Color.red;
    }

    private void OnGUI()
    {
        string debugEpisode = $"Episide: {_snakeAgent.cumulativeReward} - Step: {_snakeAgent.StepCount}";
        string debugReward = $"Reward: {_snakeAgent.cumulativeReward.ToString()}";

        GUIStyle rewardStyle = _snakeAgent.cumulativeReward < 0 ? _negativeStyle : _positiveStyle;

        GUI.Label(new Rect(20, 20, 500, 30), debugEpisode, _defualtStyle);
        GUI.Label(new Rect(20, 60, 500, 30), debugReward, rewardStyle);
    }

    // Update is called once per frame
    void Update()
    {

    }
}

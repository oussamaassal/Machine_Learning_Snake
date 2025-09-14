using UnityEngine;

public class GUI_TurtleAgent : MonoBehaviour
{
    [SerializeField] private TurtleAgent _turtleAgent;

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
        string debugEpisode = $"Episide: {_turtleAgent.cumulativeReward} - Step: {_turtleAgent.StepCount}";
        string debugReward = $"Reward: {_turtleAgent.cumulativeReward.ToString()}";

        GUIStyle rewardStyle = _turtleAgent.cumulativeReward < 0 ? _negativeStyle : _positiveStyle;

        GUI.Label(new Rect(20, 20, 500, 30), debugEpisode, _defualtStyle);
        GUI.Label(new Rect(20, 60, 500, 30), debugReward, rewardStyle);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

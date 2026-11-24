using UnityEngine;
using UnityEngine.InputSystem;

public class inputButton
{
    public bool held;           //ボタンが押されている間 true
    public bool pressed;        //押したフレームだけ true
    public bool released;       //離したフレームだけ true
    public double pressDsp;     //最後に押した時刻
    public double releaseDsp;   //最後に話した時刻

    public inputButton()
    {
        pressDsp = double.NegativeInfinity;
        releaseDsp = double.NegativeInfinity;
    }

    //毎フレーム1回現在の押下状態を渡して更新する
    public void Tick(double nowDsp, bool down)
    {
        pressed = down && !held;
        released = !down && held;

        if (pressed) pressDsp = nowDsp;
        if (released) releaseDsp = nowDsp;

        held = down;
    }
}

//すべての入力をここで読む 他のスクリプトはここを見る
[DefaultExecutionOrder(-10)]
public class playerInput : MonoBehaviour
{
    public inputButton attack = new inputButton();  //主攻撃 左クリック／R1
    public inputButton sub = new inputButton();     //サブ入力 右クリック／L1
    public inputButton guard = new inputButton();   //ガード C／R2
    public inputButton parry = new inputButton();   //パリィ F／L2

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        var pad = Gamepad.current;
        double now = AudioSettings.dspTime;

        attack.Tick(now, (mouse != null && mouse.leftButton.isPressed) || (pad != null && pad.rightShoulder.isPressed));
        sub.Tick(now, (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftShoulder.isPressed));
        guard.Tick(now, (kb != null && kb.cKey.isPressed) || (pad != null && pad.rightTrigger.isPressed));
        parry.Tick(now, (kb != null && kb.fKey.isPressed) || (pad != null && pad.leftTrigger.isPressed));
    }
}

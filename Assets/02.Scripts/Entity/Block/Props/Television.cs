using UnityEngine;

public class Television : Prop
{
    [Header("TV Settings")]
    public bool isOn = false;
    public int currentChannel = 1;
    public int maxChannels = 10;
    public float volume = 0.5f;
    public bool isMuted = false;
    
    private void Start()
    {
        InitializeChannels();
    }
    
    private void InitializeChannels()
    {
        // 채널 초기화 (실제로는 더 복잡한 채널 시스템 구현 가능)
    }
    
    public void TurnOn()
    {
        if (!isOn)
        {
            isOn = true;
        }
    }
    
    public void TurnOff()
    {
        if (isOn)
        {
            isOn = false;
        }
    }
    
    public void ChangeChannel(int channel)
    {
        if (channel >= 1 && channel <= maxChannels)
        {
            currentChannel = channel;
        }
    }
    
    public void NextChannel()
    {
        currentChannel = (currentChannel % maxChannels) + 1;
    }
    
    public void PreviousChannel()
    {
        currentChannel = currentChannel > 1 ? currentChannel - 1 : maxChannels;
    }
    
    public void SetVolume(float newVolume)
    {
        if (newVolume >= 0f && newVolume <= 1f)
        {
            volume = newVolume;
        }
    }
    
    public void ToggleMute()
    {
        isMuted = !isMuted;
    }
    
    public string GetCurrentChannelName()
    {
        return $"채널 {currentChannel}";
    }
    
    public override string Get()
    {
        if (!isOn)
        {
            return "텔레비전이 꺼져있습니다.";
        }
        
        string status = $"텔레비전이 켜져있습니다. 채널: {GetCurrentChannelName()}";
        
        if (isMuted)
        {
            status += ", 음소거";
        }
        else
        {
            status += $", 볼륨: {(volume * 100):F0}%";
        }
        
        return status;
    }
    
    public override string Interact(Actor actor)
    {
        if (isOn)
        {
            TurnOff();
            return "텔레비전을 끄겠습니다.";
        }
        else
        {
            TurnOn();
            return $"텔레비전을 켭니다. 채널: {GetCurrentChannelName()}";
        }
    }
}

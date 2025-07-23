using UnityEngine;

public class House : Building
{
    public string ownerName;

    public override string Interact(Actor actor)
    {
        base.Interact(actor);

        Debug.Log($"{actor.Name}이(가) {ownerName}의 집({Name})에 방문하여 지정된 위치로 이동합니다.");
        return $"{actor.Name}이(가) {ownerName}의 집({Name})에 방문하여 지정된 위치로 이동합니다.";
    }

    public override string Get()
    {
        throw new System.NotImplementedException();
    }
}

using UnityEngine;

public abstract class Block : Entity, IInteractable
{
    public string Description;
    public Transform toMovePos; // Move함수로 이동했을 때 도착하는 곳

    public abstract string Interact(Actor actor);
}

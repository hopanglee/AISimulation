using UnityEngine;

[System.Serializable]
public class Knife : Item
{
    public override string Get()
    {
        throw new System.NotImplementedException();
    }

    public override string Use(Actor actor, object variable)
    {
        if (variable is Entity target)
        {
            if (target is Actor actorTarget)
            {
                return Kill(actor, actorTarget);
            }
            else if (target is Block blockTarget)
            {
                return Cut(blockTarget);
            }
            else if (target is Item itemTarget)
            {
                return Cut(itemTarget);
            }
            else
            {
                return "The target type cannot be used.";
            }
        }
        return "Invalid input value.";
    }

    private string Kill(Actor actor, Actor target)
    {
        target.Death();
        if (actor == target)
        {
            return "They stabbed themselves and committed suicide.";
        }
        else
        {
            // Example kill logic
            return $"Slashed {target.Name} to death with a knife.";
        }
    }

    private string Cut(Item item)
    {
        // Example logic for cutting an item
        return $"Cut the {item.Name} with a knife.";
    }

    private string Cut(Block block)
    {
        // Example logic for cutting a block
        return $"Cut the {block.Name} with a knife.";
    }
}

using UnityEngine;
using UnityEngine.Tilemaps;

// Connect hand-painted slope tiles to the automatic soil/grass edges as well.
public sealed class Chapter1TerrainTile : RuleTile
{
    public override bool RuleMatch(int neighbor,TileBase other)
    {
        var tile=other as Tile;
        bool connected=other==this||(tile!=null&&tile.colliderType!=Tile.ColliderType.None&&
            tile.sprite!=null&&m_DefaultSprite!=null&&tile.sprite.texture==m_DefaultSprite.texture);
        if(neighbor==TilingRuleOutput.Neighbor.This)return connected;
        if(neighbor==TilingRuleOutput.Neighbor.NotThis)return !connected;
        return base.RuleMatch(neighbor,other);
    }
}

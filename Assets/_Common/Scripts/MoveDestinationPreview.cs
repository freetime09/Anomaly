using UnityEngine;

// Queries the live collision world without moving the player or firing triggers.
public static class MoveDestinationPreview
{
    static readonly RaycastHit2D[] hits = new RaycastHit2D[48];
    static readonly Collider2D[] overlaps = new Collider2D[32];

    static bool Solid(Collider2D other, BoxCollider2D player, bool horizontal)
    {
        if (other == null || other.isTrigger || other == player || other.transform.IsChildOf(player.transform)) return false;
        if (other.attachedRigidbody != null && other.attachedRigidbody == player.attachedRigidbody) return false;
        if (Physics2D.GetIgnoreCollision(other, player)) return false;
        var platform = other.GetComponent<PlatformEffector2D>();
        return !(horizontal && platform != null && platform.useOneWay);
    }

    static bool Sweep(Vector2 center, Vector2 size, Vector2 direction, float distance, int mask,
                      BoxCollider2D player, bool horizontal, out RaycastHit2D nearest)
    {
        nearest = default(RaycastHit2D);
        float closest = float.PositiveInfinity;
        // Sample the leading face rather than BoxCast: box sweeps in this project's
        // Unity version return no hits even against a plain BoxCollider2D.
        for (int ray = 0; ray < 3; ray++)
        {
            Vector2 origin = center;
            if (Mathf.Abs(direction.x) > .5f)
                origin += new Vector2(direction.x * size.x * .5f, (ray - 1) * (size.y * .5f - .015f));
            else origin += new Vector2((ray - 1) * (size.x * .5f - .015f), direction.y * size.y * .5f);
            int count = Physics2D.RaycastNonAlloc(origin, direction, hits, distance, mask);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!Solid(hit.collider, player, horizontal) || hit.distance < .0001f) continue;
                if (Vector2.Dot(hit.normal, direction) > -.01f) continue;
                if (hit.distance < closest) { closest = hit.distance; nearest = hit; }
            }
        }
        return nearest.collider != null;
    }

    static bool Clear(Vector2 center, Vector2 size, int mask, BoxCollider2D player)
    {
        int count = Physics2D.OverlapBoxNonAlloc(center, size, 0, overlaps, mask);
        for (int i = 0; i < count; i++)
            if (Solid(overlaps[i], player, true)) return false;
        return true;
    }

    public static bool TryResolve(PlayerController player, int direction, float distance, out Vector3 destination, float movementSpeed = -1)
    {
        var box = player.GetComponent<BoxCollider2D>();
        var body = player.GetComponent<Rigidbody2D>();
        Bounds bounds = box.bounds;
        Vector2 start = bounds.center, center = start;
        Vector2 size = new Vector2(Mathf.Max(.04f, bounds.size.x - .04f), Mathf.Max(.04f, bounds.size.y - .04f));
        int mask = Physics2D.GetLayerCollisionMask(player.gameObject.layer);
        float speed = Mathf.Max(.1f, movementSpeed > 0 ? movementSpeed : player.moveSpeed * player.RespawnMoveScale);
        float remaining = Mathf.Max(0, distance);
        float vy = body.linearVelocity.y;
        float gravity = Physics2D.gravity.y * body.gravityScale;
        float floorNormal = Mathf.Cos(Mathf.Clamp(player.maxSlopeAngle, 0, 80) * Mathf.Deg2Rad);
        RaycastHit2D support;
        bool grounded = vy <= .1f && Sweep(center, size, Vector2.down, .12f, mask, box, false, out support);
        const float dt = .02f;
        // Same requested horizontal distance, sampled along the changing terrain height.
        for (int step = 0; step < 500 && remaining > .001f; step++)
        {
            float dx = Mathf.Min(speed * dt, remaining);
            Vector2 next = center + Vector2.right * (direction * dx);
            bool snapped = false;
            if (grounded)
            {
                float rise = player.maxStepHeight + .04f;
                RaycastHit2D floor;
                if (Sweep(next + Vector2.up * rise, size, Vector2.down, rise + .17f, mask, box, false, out floor)
                    && floor.normal.y >= floorNormal)
                {
                    Vector2 candidate = next + Vector2.up * (rise - floor.distance + .008f);
                    if (candidate.y - center.y <= player.maxStepHeight + .025f && Clear(candidate, size * .995f, mask, box))
                    { next.y = candidate.y; snapped = true; }
                }
            }
            RaycastHit2D wall;
            Vector2 horizontalStart = new Vector2(center.x, Mathf.Max(center.y, next.y));
            bool blocked = Sweep(horizontalStart, size, Vector2.right * direction, dx + .01f, mask, box, true, out wall)
                && wall.normal.y < floorNormal;
            if (blocked)
            {
                next.x = center.x + direction * Mathf.Max(0, wall.distance - .01f);
                next.y = center.y;
                remaining = 0;
                snapped = false;
            }
            else remaining -= dx;
            center = next;
            if (snapped) { vy = 0; grounded = true; }
            else
            {
                vy += gravity * dt;
                Vector2 verticalDirection = vy > 0 ? Vector2.up : Vector2.down;
                float verticalDistance = Mathf.Abs(vy * dt);
                RaycastHit2D vertical;
                if (Sweep(center, size, verticalDirection, verticalDistance + .015f, mask, box, vy > 0, out vertical))
                {
                    center += verticalDirection * Mathf.Max(0, vertical.distance - .008f);
                    grounded = vy <= 0 && vertical.normal.y >= floorNormal;
                    vy = 0;
                }
                else { center += Vector2.up * (vy * dt); grounded = false; }
            }
        }
        // The marker denotes where the character finishes standing, including the fall after a descent.
        RaycastHit2D landing;
        bool valid = Sweep(center + Vector2.up * .04f, size, Vector2.down, 40, mask, box, false, out landing)
                     && landing.normal.y >= floorNormal;
        if (valid) center.y += .04f - landing.distance + .008f;
        destination = player.transform.position + (Vector3)(center - start);
        return valid;
    }
}

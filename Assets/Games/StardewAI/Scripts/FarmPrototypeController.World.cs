using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using HuanYouYu.MiniGameHall;

namespace FarmPrototype
{
    public sealed partial class FarmPrototypeController
    {
        private bool TryPlanQueuedPath(Vector2Int goalCell)
        {
            Vector2Int startCell = WorldToWalkCell(_playerPosition);
            if (!IsWalkCellInBounds(startCell) || !IsWalkCellInBounds(goalCell) || _blockedWalkCells.Contains(goalCell))
            {
                return false;
            }

            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            frontier.Enqueue(startCell);
            cameFrom[startCell] = startCell;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                if (current == goalCell)
                {
                    break;
                }

                for (int i = 0; i < _walkNeighbors.Length; i++)
                {
                    Vector2Int next = current + _walkNeighbors[i];
                    if (!IsWalkCellInBounds(next) || _blockedWalkCells.Contains(next) || cameFrom.ContainsKey(next))
                    {
                        continue;
                    }

                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

            if (!cameFrom.ContainsKey(goalCell))
            {
                return false;
            }

            _queuedPathCells.Clear();
            Vector2Int trace = goalCell;
            while (trace != startCell)
            {
                _queuedPathCells.Add(trace);
                trace = cameFrom[trace];
            }

            _queuedPathCells.Reverse();
            return _queuedPathCells.Count > 0;
        }

        private bool AdvanceQueuedWaypoint()
        {
            _queuedPathIndex++;
            if (_queuedPathIndex >= _queuedPathCells.Count)
            {
                return false;
            }

            RefreshQueuedMoveTarget();
            return true;
        }

        private void RefreshQueuedMoveTarget()
        {
            if (_queuedPathIndex >= 0 && _queuedPathIndex < _queuedPathCells.Count)
            {
                _queuedMoveTargetWorld = WalkCellToWorld(_queuedPathCells[_queuedPathIndex]);
            }
        }

        private Vector2 ResolveBlockedMovement(Vector2 previousPosition, Vector2 desiredPosition)
        {
            Vector2 resolved = previousPosition;
            Vector2 clamped = new Vector2(
                Mathf.Clamp(desiredPosition.x, WalkMinX + 1.2f, WalkMaxX - 1.2f),
                Mathf.Clamp(desiredPosition.y, WalkMinY + 1.9f, WalkMaxY - 2.4f));

            resolved.x = clamped.x;
            if (IsBlockedAt(resolved))
            {
                resolved.x = previousPosition.x;
            }

            resolved.y = clamped.y;
            if (IsBlockedAt(resolved))
            {
                resolved.y = previousPosition.y;
            }

            return IsBlockedAt(resolved) ? previousPosition : resolved;
        }

        private Vector3 GetClampedCameraPosition()
        {
            if (_mainCamera == null)
            {
                return new Vector3(_playerPosition.x + 0.35f, _playerPosition.y + 0.15f, -10f);
            }

            float halfHeight = _mainCamera.orthographicSize;
            float halfWidth = halfHeight * _mainCamera.aspect;
            float mapMinX = WalkMinX - 0.5f;
            float mapMaxX = WalkMaxX + 0.5f;
            float mapMinY = WalkMinY - 0.5f;
            float mapMaxY = WalkMaxY + 0.5f;
            float targetX = _playerPosition.x + 0.35f;
            float targetY = _playerPosition.y + 0.15f;

            float clampedX = ClampCameraAxis(targetX, mapMinX, mapMaxX, halfWidth);
            float clampedY = ClampCameraAxis(targetY, mapMinY, mapMaxY, halfHeight);
            return new Vector3(clampedX, clampedY, -10f);
        }

        private static float ClampCameraAxis(float target, float min, float max, float halfExtent)
        {
            float visibleSpan = halfExtent * 2f;
            float worldSpan = max - min;
            if (visibleSpan >= worldSpan)
            {
                return (min + max) * 0.5f;
            }

            return Mathf.Clamp(target, min + halfExtent, max - halfExtent);
        }

        private void AddBlockedRect(float x, float y, float width, float height)
        {
            _blockedWorldRects.Add(new Rect(x - PlayerCollisionRadius, y - PlayerCollisionRadius, width + (PlayerCollisionRadius * 2f), height + (PlayerCollisionRadius * 2f)));
        }

        private void AddFenceCollisionBlocks(int fenceLeft, int fenceRight, int fenceBottom, int fenceTop)
        {
            for (int y = fenceBottom; y <= fenceTop; y++)
            {
                for (int x = fenceLeft; x <= fenceRight; x++)
                {
                    if (!HasFenceAt(x, y))
                    {
                        continue;
                    }

                    AddBlockedRect(x, y, 1f, 1f);
                }
            }
        }

        private void EnsurePlayerSpawnPosition()
        {
            Vector2Int preferredCell = WorldToWalkCell(_playerPosition);
            Vector2Int safeCell = FindNearestWalkableCell(preferredCell);
            _playerPosition = WalkCellToWorld(safeCell);
        }

        private Vector2Int FindNearestWalkableCell(Vector2Int origin)
        {
            Vector2Int start = new Vector2Int(
                Mathf.Clamp(origin.x, WalkMinX, WalkMaxX),
                Mathf.Clamp(origin.y, WalkMinY, WalkMaxY));

            if (!_blockedWalkCells.Contains(start))
            {
                return start;
            }

            Queue<Vector2Int> frontier = new Queue<Vector2Int>();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            frontier.Enqueue(start);
            visited.Add(start);

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                for (int i = 0; i < _walkNeighbors.Length; i++)
                {
                    Vector2Int next = current + _walkNeighbors[i];
                    if (!IsWalkCellInBounds(next) || visited.Contains(next))
                    {
                        continue;
                    }

                    if (!_blockedWalkCells.Contains(next))
                    {
                        return next;
                    }

                    visited.Add(next);
                    frontier.Enqueue(next);
                }
            }

            return new Vector2Int(0, -4);
        }

        private void AddWaterCollisionBlocks()
        {
            TileBase waterTile = FarmPixelArtFactory.GetTile(FarmTileArt.Water);
            for (int y = WalkMinY; y <= WalkMaxY; y++)
            {
                for (int x = WalkMinX; x <= WalkMaxX; x++)
                {
                    TileBase tile = _terrainTilemap.GetTile(new Vector3Int(x, y, 0));
                    if (tile == waterTile)
                    {
                        AddBlockedRect(x, y, 1f, 1f);
                    }
                }
            }
        }

        private bool HasFenceAt(int x, int y)
        {
            TileBase fenceTile = FarmPixelArtFactory.GetTile(FarmTileArt.Fence);
            TileBase detailTile = _detailTilemap.GetTile(new Vector3Int(x, y, 0));
            return detailTile == fenceTile;
        }

        private bool IsBlockedAt(Vector2 worldPosition)
        {
            for (int i = 0; i < _blockedWorldRects.Count; i++)
            {
                if (_blockedWorldRects[i].Contains(worldPosition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointNearRect(Vector2 point, Rect rect)
        {
            Rect expanded = new Rect(rect.xMin - 0.25f, rect.yMin - 0.25f, rect.width + 0.5f, rect.height + 0.5f);
            return expanded.Contains(point);
        }

        private static Vector2 WalkCellToWorld(Vector2Int walkCell)
        {
            return new Vector2(walkCell.x + 0.5f, walkCell.y + 0.5f);
        }

        private static Vector2Int WorldToWalkCell(Vector2 worldPosition)
        {
            return new Vector2Int(
                Mathf.RoundToInt(worldPosition.x - 0.5f),
                Mathf.RoundToInt(worldPosition.y - 0.5f));
        }

        private static bool IsWalkCellInBounds(Vector2Int walkCell)
        {
            return walkCell.x >= WalkMinX && walkCell.x <= WalkMaxX && walkCell.y >= WalkMinY && walkCell.y <= WalkMaxY;
        }

        private void SetActiveTool(ToolType tool)
        {
            if (_activeTool == tool)
            {
                return;
            }

            _activeTool = tool;
            SetMessage(UiTextCatalog.Format("stardewai.msg.tool_switched", GetToolLabel(tool)));
        }

        private Vector2 ReadMovement()
        {
            Vector2 move = Vector2.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                move.x -= 1f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                move.x += 1f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                move.y -= 1f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                move.y += 1f;
            }

            return move;
        }

        private void UpdatePlayerMovement(Vector2 moveInput)
        {
            _lastPlayerMovementDelta = Vector2.zero;
            bool hasManualInput = moveInput.sqrMagnitude > 0.0001f;
            if (hasManualInput)
            {
                CancelQueuedMouseAction();
            }
            else if (_hasQueuedMouseAction)
            {
                while (_hasQueuedMouseAction)
                {
                    Vector2 remaining = _queuedMoveTargetWorld - _playerPosition;
                    if (remaining.sqrMagnitude > AutoMoveArrivalDistance * AutoMoveArrivalDistance)
                    {
                        moveInput = remaining.normalized;
                        break;
                    }

                    _playerPosition = _queuedMoveTargetWorld;
                    if (!AdvanceQueuedWaypoint())
                    {
                        _lastPlayerMovementDelta = Vector2.zero;
                        UpdatePlayerVisual(Vector2.zero);
                        CompleteQueuedMouseAction();
                        return;
                    }

                    UpdatePlayerVisual(Vector2.zero);
                }
            }

            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }

            Vector2 previousPosition = _playerPosition;
            Vector2 nextPosition = previousPosition + (moveInput * MoveSpeed * Time.deltaTime);
            nextPosition = ResolveBlockedMovement(previousPosition, nextPosition);
            _playerPosition = nextPosition;

            Vector2 delta = nextPosition - previousPosition;
            _lastPlayerMovementDelta = delta;
            if (delta.sqrMagnitude > 0.0001f)
            {
                _facing = DominantDirection(delta);
                UpdatePlayerVisual(delta);
            }

            if (_hasQueuedMouseAction)
            {
                Vector2 remaining = _queuedMoveTargetWorld - _playerPosition;
                if (remaining.sqrMagnitude <= AutoMoveArrivalDistance * AutoMoveArrivalDistance)
                {
                    Vector2 snappedDelta = _queuedMoveTargetWorld - _playerPosition;
                    _playerPosition = _queuedMoveTargetWorld;
                    if (!AdvanceQueuedWaypoint())
                    {
                        _lastPlayerMovementDelta = snappedDelta;
                        UpdatePlayerVisual(snappedDelta);
                        CompleteQueuedMouseAction();
                    }
                    else
                    {
                        _lastPlayerMovementDelta = snappedDelta;
                        UpdatePlayerVisual(snappedDelta);
                    }
                }
            }
        }

        private void UpdatePlayerVisual(Vector2 movementDelta)
        {
            if (_playerRenderer == null || _playerShadow == null)
            {
                return;
            }

            bool isMoving = movementDelta.sqrMagnitude > 0.0001f;
            bool useSideWalkSkeleton = isMoving && _facing.x != 0;
            _playerRenderer.sprite = FarmPixelArtFactory.GetSprite(useSideWalkSkeleton
                ? FarmSpriteArt.PlayerSideBody
                : GetCurrentPlayerSprite(isMoving));

            if (_facing.x != 0)
            {
                _playerRenderer.flipX = _facing.x < 0;
            }
            else
            {
                _playerRenderer.flipX = false;
            }

            float bobOffset = isMoving
                ? Mathf.Abs(Mathf.Sin(Time.time * PlayerWalkAnimationRate * Mathf.PI * 0.5f)) * PlayerWalkBobAmplitude
                : Mathf.Sin(Time.time * 2.1f) * PlayerIdleBobAmplitude;
            float actionProgress = GetToolActionProgress();
            float bodyTilt = GetToolActionBodyTilt(actionProgress);
            float bodyStretch = actionProgress > 0f ? 1f + (Mathf.Sin(actionProgress * Mathf.PI) * 0.04f) : 1f;

            float sideWalkPhase = useSideWalkSkeleton
                ? Mathf.Sin(Time.time * PlayerWalkAnimationRate * Mathf.PI)
                : 0f;
            float visualBodyTilt = bodyTilt + (useSideWalkSkeleton
                ? sideWalkPhase * PlayerSideWalkBodyTilt * (_facing.x < 0f ? -1f : 1f)
                : 0f);

            _playerRenderer.transform.position = _playerPosition + new Vector2(0f, bobOffset);
            _playerRenderer.transform.rotation = Quaternion.Euler(0f, 0f, visualBodyTilt);
            _playerRenderer.transform.localScale = new Vector3(bodyStretch, 1f + ((bodyStretch - 1f) * 0.3f), 1f);
            _playerShadow.transform.position = _playerPosition + new Vector2(0f, -0.34f);

            int sortBase = GetActorSortBase(_playerPosition.y);
            _playerShadow.sortingOrder = sortBase - 1;
            _playerRenderer.sortingOrder = sortBase;
            UpdatePlayerSideWalkSkeleton(useSideWalkSkeleton, sideWalkPhase, bobOffset, visualBodyTilt, bodyStretch, sortBase);
            if (useSideWalkSkeleton)
            {
                _playerShadow.sortingOrder = sortBase - 3;
            }

            if (isMoving)
            {
                float shadowScale = 0.92f + (Mathf.Abs(Mathf.Sin(Time.time * PlayerWalkAnimationRate * Mathf.PI * 0.5f)) * 0.1f);
                _playerShadow.transform.localScale = new Vector3(shadowScale, shadowScale, 1f);
            }
            else
            {
                float idleScale = 0.98f + (Mathf.Sin(Time.time * 2.1f) * 0.02f);
                _playerShadow.transform.localScale = new Vector3(idleScale, idleScale, 1f);
            }
        }

        private void UpdatePlayerSideWalkSkeleton(
            bool isActive,
            float phase,
            float bobOffset,
            float bodyTilt,
            float bodyStretch,
            int sortBase)
        {
            if (_playerSideFrontLegRoot == null ||
                _playerSideBackLegRoot == null ||
                _playerSideFrontLegRenderer == null ||
                _playerSideBackLegRenderer == null)
            {
                return;
            }

            _playerSideFrontLegRenderer.enabled = isActive;
            _playerSideBackLegRenderer.enabled = isActive;
            if (!isActive)
            {
                _playerSideFrontLegRoot.rotation = Quaternion.identity;
                _playerSideBackLegRoot.rotation = Quaternion.identity;
                return;
            }

            float facingSign = _facing.x < 0f ? -1f : 1f;
            UpdateSideWalkLegRenderer(
                _playerSideFrontLegRenderer,
                phase,
                facingSign,
                bobOffset,
                bodyTilt,
                bodyStretch,
                sortBase + 1);
            UpdateSideWalkLegRenderer(
                _playerSideBackLegRenderer,
                -phase,
                facingSign,
                bobOffset,
                bodyTilt,
                bodyStretch,
                sortBase - 2);
        }

        private void UpdateSideWalkLegRenderer(
            SpriteRenderer renderer,
            float phase,
            float facingSign,
            float bobOffset,
            float bodyTilt,
            float bodyStretch,
            int sortingOrder)
        {
            Transform legRoot = renderer.transform.parent;
            renderer.flipX = facingSign < 0f;
            renderer.transform.localPosition = new Vector3(0f, -PlayerSideWalkHipOffsetY, 0f);
            renderer.transform.localRotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one;
            legRoot.position = _playerPosition + new Vector2(0f, bobOffset + PlayerSideWalkHipOffsetY);
            legRoot.rotation = Quaternion.Euler(0f, 0f, bodyTilt + (phase * PlayerSideWalkLegTilt * facingSign));
            legRoot.localScale = new Vector3(bodyStretch, 1f, 1f);
            renderer.sortingOrder = sortingOrder;
        }

        private void UpdateToolActionAnimation()
        {
            if (_playerToolRenderer == null)
            {
                return;
            }

            if (_toolActionTimer <= 0f)
            {
                _playerToolRenderer.enabled = false;
                _playerToolRenderer.transform.rotation = Quaternion.identity;
                _playerToolRenderer.transform.localScale = Vector3.one;
                return;
            }

            _toolActionTimer = Mathf.Max(0f, _toolActionTimer - Time.deltaTime);
            float progress = GetToolActionProgress();
            Vector2 facing = new Vector2(_toolActionFacing.x, _toolActionFacing.y);
            Vector2 sideways = new Vector2(facing.y, -facing.x);
            float reach = Mathf.Lerp(0.22f, 0.72f, Mathf.Sin(progress * Mathf.PI * 0.5f));
            float sweep = Mathf.Sin(progress * Mathf.PI) * 0.12f;
            float lift = 0.16f + (Mathf.Sin(progress * Mathf.PI) * 0.08f);
            Vector2 toolPosition = _playerPosition + (facing * reach) + (sideways * sweep) + new Vector2(0f, lift);

            _playerToolRenderer.enabled = true;
            _playerToolRenderer.sprite = FarmPixelArtFactory.GetSprite(GetToolActionSprite(_toolActionTool));
            _playerToolRenderer.flipX = _toolActionFacing.x < 0;
            _playerToolRenderer.transform.position = toolPosition;
            _playerToolRenderer.transform.rotation = Quaternion.Euler(0f, 0f, GetToolActionRotation(progress));
            _playerToolRenderer.transform.localScale = Vector3.one * (0.98f + (Mathf.Sin(progress * Mathf.PI) * 0.08f));

            int sortBase = GetActorSortBase(_playerPosition.y);
            _playerToolRenderer.sortingOrder = _toolActionFacing.y > 0 ? sortBase - 1 : sortBase + 1;

            if (_toolActionTimer <= 0f)
            {
                _playerToolRenderer.enabled = false;
            }
        }

        private void UpdateToolHitEffect()
        {
            if (_toolHitEffectRenderer == null)
            {
                return;
            }

            if (_toolHitEffectTimer <= 0f)
            {
                _toolHitEffectRenderer.enabled = false;
                return;
            }

            _toolHitEffectTimer = Mathf.Max(0f, _toolHitEffectTimer - Time.deltaTime);
            float progress = 1f - Mathf.Clamp01(_toolHitEffectTimer / ToolHitEffectDuration);
            float scale = Mathf.Lerp(0.66f, GetToolHitEffectPeakScale(_toolHitEffectTool), Mathf.Sin(progress * Mathf.PI * 0.5f));
            float alpha = Mathf.Lerp(0.98f, 0f, progress);
            Vector2 rise = new Vector2(0f, Mathf.Lerp(0f, 0.22f, progress));

            _toolHitEffectRenderer.enabled = true;
            _toolHitEffectRenderer.transform.position = _toolHitEffectWorldPosition + rise;
            _toolHitEffectRenderer.transform.localScale = Vector3.one * scale;

            Color color = Color.white;
            color.a = alpha;
            _toolHitEffectRenderer.color = color;

            if (_toolHitEffectTimer <= 0f)
            {
                _toolHitEffectRenderer.enabled = false;
            }
        }

        private FarmSpriteArt GetCurrentPlayerSprite(bool isMoving)
        {
            int frame = isMoving
                ? Mathf.FloorToInt(Time.time * PlayerWalkAnimationRate) % 4
                : 0;

            if (_facing.x != 0)
            {
                return GetAnimatedDirectionalSprite(
                    FarmSpriteArt.PlayerSideStepA,
                    FarmSpriteArt.PlayerSideStepB,
                    frame);
            }

            if (_facing.y >= 0)
            {
                return GetAnimatedDirectionalSprite(
                    FarmSpriteArt.PlayerUpStepA,
                    FarmSpriteArt.PlayerUpStepB,
                    frame);
            }

            return GetAnimatedDirectionalSprite(
                FarmSpriteArt.PlayerDownStepA,
                FarmSpriteArt.PlayerDownStepB,
                frame);
        }

        private static FarmSpriteArt GetAnimatedDirectionalSprite(
            FarmSpriteArt stepA,
            FarmSpriteArt stepB,
            int frame)
        {
            return frame % 2 == 0 ? stepA : stepB;
        }

        private void TriggerToolActionAnimation(ToolType tool)
        {
            _toolActionTool = tool;
            _toolActionFacing = _facing;
            _toolActionTimer = ToolActionDuration;
        }

        private float GetToolActionProgress()
        {
            if (_toolActionTimer <= 0f)
            {
                return 0f;
            }

            return 1f - Mathf.Clamp01(_toolActionTimer / ToolActionDuration);
        }

        private FarmSpriteArt GetToolActionSprite(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hoe:
                    return FarmSpriteArt.ToolHoe;
                case ToolType.WateringCan:
                    return FarmSpriteArt.ToolWateringCan;
                case ToolType.Seeds:
                    return FarmSpriteArt.ToolSeedBag;
                case ToolType.Harvest:
                    return FarmSpriteArt.ToolSickle;
                default:
                    return FarmSpriteArt.ToolHoe;
            }
        }

        private float GetToolActionBodyTilt(float progress)
        {
            if (progress <= 0f)
            {
                return 0f;
            }

            float swing = Mathf.Sin(progress * Mathf.PI);
            if (_toolActionFacing.x != 0)
            {
                return -_toolActionFacing.x * Mathf.Lerp(6f, 13f, swing);
            }

            return _toolActionFacing.y > 0
                ? Mathf.Lerp(-4f, -9f, swing)
                : Mathf.Lerp(4f, 10f, swing);
        }

        private float GetToolActionRotation(float progress)
        {
            float swing = Mathf.Sin(progress * Mathf.PI);

            switch (_toolActionTool)
            {
                case ToolType.Hoe:
                    return GetDirectionalRotation(-32f, 52f, progress, swing);
                case ToolType.WateringCan:
                    return GetDirectionalRotation(18f, -26f, progress, swing);
                case ToolType.Seeds:
                    return GetDirectionalRotation(-12f, 22f, progress, swing);
                case ToolType.Harvest:
                    return GetDirectionalRotation(-58f, 72f, progress, swing);
                default:
                    return 0f;
            }
        }

        private float GetDirectionalRotation(float startAngle, float endAngle, float progress, float swing)
        {
            float swingAngle = Mathf.Lerp(startAngle, endAngle, progress) + (swing * 6f);

            if (_toolActionFacing.x < 0)
            {
                return 180f - swingAngle;
            }

            if (_toolActionFacing.x > 0)
            {
                return swingAngle;
            }

            if (_toolActionFacing.y > 0)
            {
                return 90f + swingAngle;
            }

            return -90f + swingAngle;
        }

        private void TriggerToolHitEffect(Vector2Int grid, ToolType tool)
        {
            if (_toolHitEffectRenderer == null)
            {
                return;
            }

            _toolHitEffectTool = tool;
            _toolHitEffectTimer = ToolHitEffectDuration;
            _toolHitEffectWorldPosition = GridToWorld(grid) + GetToolHitEffectOffset(tool);
            _toolHitEffectRenderer.sprite = FarmPixelArtFactory.GetSprite(GetToolHitEffectSprite(tool));
            _toolHitEffectRenderer.transform.position = _toolHitEffectWorldPosition;
            _toolHitEffectRenderer.transform.localScale = Vector3.one * 0.66f;
            _toolHitEffectRenderer.color = Color.white;
            _toolHitEffectRenderer.enabled = true;

            int sortBase = GetActorSortBase(_toolHitEffectWorldPosition.y);
            _toolHitEffectRenderer.sortingOrder = sortBase + 2;
        }

        private static FarmSpriteArt GetToolHitEffectSprite(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hoe:
                    return FarmSpriteArt.EffectHoeHit;
                case ToolType.WateringCan:
                    return FarmSpriteArt.EffectWaterHit;
                case ToolType.Seeds:
                    return FarmSpriteArt.EffectSeedHit;
                case ToolType.Harvest:
                    return FarmSpriteArt.EffectHarvestHit;
                default:
                    return FarmSpriteArt.EffectHoeHit;
            }
        }

        private static Vector2 GetToolHitEffectOffset(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.WateringCan:
                    return new Vector2(0f, 0.16f);
                case ToolType.Seeds:
                    return new Vector2(0f, 0.08f);
                case ToolType.Harvest:
                    return new Vector2(0f, 0.18f);
                default:
                    return new Vector2(0f, 0.1f);
            }
        }

        private static float GetToolHitEffectPeakScale(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.WateringCan:
                    return 1.14f;
                case ToolType.Harvest:
                    return 1.08f;
                case ToolType.Seeds:
                    return 0.96f;
                default:
                    return 1f;
            }
        }

        private AudioClip GetToolHitClip(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hoe:
                    return _hoeHitClip;
                case ToolType.WateringCan:
                    return _waterHitClip;
                case ToolType.Seeds:
                    return _seedHitClip;
                case ToolType.Harvest:
                    return _harvestHitClip;
                default:
                    return _tileClickClip;
            }
        }

        private static float GetToolHitVolume(ToolType tool)
        {
            switch (tool)
            {
                case ToolType.Hoe:
                    return 0.92f;
                case ToolType.WateringCan:
                    return 0.8f;
                case ToolType.Seeds:
                    return 0.68f;
                case ToolType.Harvest:
                    return 0.9f;
                default:
                    return 0.85f;
            }
        }

        private void UpdateNpcMovement()
        {
            if (_npcRenderer == null || _npcShadow == null ||
                _wandererNpcRenderer == null || _wandererNpcShadow == null ||
                _merchantNpcRenderer == null || _merchantNpcShadow == null ||
                _fisherNpcRenderer == null || _fisherNpcShadow == null ||
                _troubleNpcRenderer == null || _troubleNpcShadow == null)
            {
                return;
            }

            if (_isDialogueOpen || _isMerchantShopOpen)
            {
                UpdateNpcVisual();
                return;
            }

            _npcTargetPosition = GetNpcScheduledPosition(GetCurrentTimePeriod(), NpcIdentity.Lumi);
            _npcPosition = Vector2.MoveTowards(_npcPosition, _npcTargetPosition, NpcMoveSpeed * Time.deltaTime);
            _wandererNpcTargetPosition = GetNpcScheduledPosition(GetCurrentTimePeriod(), NpcIdentity.XiaoTuanzi);
            _wandererNpcPosition = Vector2.MoveTowards(_wandererNpcPosition, _wandererNpcTargetPosition, NpcMoveSpeed * Time.deltaTime);
            _merchantNpcTargetPosition = GetNpcScheduledPosition(GetCurrentTimePeriod(), NpcIdentity.Qianran);
            _merchantNpcPosition = Vector2.MoveTowards(_merchantNpcPosition, _merchantNpcTargetPosition, NpcMoveSpeed * Time.deltaTime);
            _fisherNpcTargetPosition = GetNpcScheduledPosition(GetCurrentTimePeriod(), NpcIdentity.HaiyinAwa);
            _fisherNpcPosition = Vector2.MoveTowards(_fisherNpcPosition, _fisherNpcTargetPosition, NpcMoveSpeed * Time.deltaTime);
            _troubleNpcTargetPosition = GetNpcScheduledPosition(GetCurrentTimePeriod(), NpcIdentity.Azhai);
            _troubleNpcPosition = Vector2.MoveTowards(_troubleNpcPosition, _troubleNpcTargetPosition, NpcMoveSpeed * Time.deltaTime);
            UpdateNpcVisual();
            TrySabotageCropByTroubleNpc();
        }

        private void UpdateNpcVisual()
        {
            if (_npcRenderer == null || _npcShadow == null ||
                _wandererNpcRenderer == null || _wandererNpcShadow == null ||
                _merchantNpcRenderer == null || _merchantNpcShadow == null ||
                _fisherNpcRenderer == null || _fisherNpcShadow == null ||
                _troubleNpcRenderer == null || _troubleNpcShadow == null)
            {
                return;
            }

            _npcRenderer.transform.position = _npcPosition;
            _npcShadow.transform.position = _npcPosition + new Vector2(0f, -0.34f);

            int sortBase = GetActorSortBase(_npcPosition.y);
            _npcShadow.sortingOrder = sortBase - 1;
            _npcRenderer.sortingOrder = sortBase;

            _wandererNpcRenderer.transform.position = _wandererNpcPosition;
            _wandererNpcShadow.transform.position = _wandererNpcPosition + new Vector2(0f, -0.34f);
            int wandererSortBase = GetActorSortBase(_wandererNpcPosition.y);
            _wandererNpcShadow.sortingOrder = wandererSortBase - 1;
            _wandererNpcRenderer.sortingOrder = wandererSortBase;

            _merchantNpcRenderer.transform.position = _merchantNpcPosition;
            _merchantNpcShadow.transform.position = _merchantNpcPosition + new Vector2(0f, -0.34f);
            int merchantSortBase = GetActorSortBase(_merchantNpcPosition.y);
            _merchantNpcShadow.sortingOrder = merchantSortBase - 1;
            _merchantNpcRenderer.sortingOrder = merchantSortBase;

            _fisherNpcRenderer.transform.position = _fisherNpcPosition;
            _fisherNpcShadow.transform.position = _fisherNpcPosition + new Vector2(0f, -0.34f);
            int fisherSortBase = GetActorSortBase(_fisherNpcPosition.y);
            _fisherNpcShadow.sortingOrder = fisherSortBase - 1;
            _fisherNpcRenderer.sortingOrder = fisherSortBase;

            _troubleNpcRenderer.transform.position = _troubleNpcPosition;
            _troubleNpcShadow.transform.position = _troubleNpcPosition + new Vector2(0f, -0.34f);
            int troubleSortBase = GetActorSortBase(_troubleNpcPosition.y);
            _troubleNpcShadow.sortingOrder = troubleSortBase - 1;
            _troubleNpcRenderer.sortingOrder = troubleSortBase;
        }

        private static int GetActorSortBase(float worldY)
        {
            return ActorSortingBase + Mathf.RoundToInt(-worldY * 10f);
        }

        private void UpdateTimeOfDay()
        {
            if (_isDialogueOpen || _isMerchantShopOpen)
            {
                return;
            }

            _timeOfDayMinutes += GameMinutesPerSecond * Time.deltaTime;
            if (_timeOfDayMinutes >= DayEndMinutes)
            {
                _timeOfDayMinutes = DayEndMinutes;
                CancelQueuedMouseAction();
                AdvanceDay(true);
                return;
            }

            TimePeriod currentPeriod = GetCurrentTimePeriod();
            if (currentPeriod != _lastTimePeriod)
            {
                _lastTimePeriod = currentPeriod;
                SetMessage(UiTextCatalog.Format("stardewai.msg.time_period_changed", GetTimePeriodLabel(currentPeriod)));
            }
        }

        private void TrySabotageCropByTroubleNpc()
        {
            if (IsVillageFestivalDay())
            {
                return;
            }

            if (GetCurrentTimePeriod() == TimePeriod.Night)
            {
                return;
            }

            _cropSabotageTimer -= Time.deltaTime;
            if (_cropSabotageTimer > 0f)
            {
                return;
            }

            _cropSabotageTimer = UnityEngine.Random.Range(12f, 26f);

            FarmTile targetTile = null!;
            bool found = false;
            float bestDistance = float.MaxValue;
            bool bestIsRipe = false;

            for (int y = 0; y < FieldHeight; y++)
            {
                for (int x = 0; x < FieldWidth; x++)
                {
                    FarmTile tile = _tiles[x, y];
                    if (tile == null || !tile.HasCrop)
                    {
                        continue;
                    }

                    bool isRipe = IsCropRipe(tile);
                    float distance = Vector2.Distance(_troubleNpcPosition, GridToWorld(tile.Grid));
                    if (!found ||
                        (isRipe && !bestIsRipe) ||
                        (isRipe == bestIsRipe && distance < bestDistance))
                    {
                        targetTile = tile;
                        found = true;
                        bestDistance = distance;
                        bestIsRipe = isRipe;
                    }
                }
            }

            if (!found)
            {
                return;
            }

            ItemType removedCrop = targetTile.CropItemType;
            if (!IsCropItem(removedCrop))
            {
                removedCrop = ItemType.Parsnip;
            }

            targetTile.HasCrop = false;
            targetTile.CropItemType = ItemType.None;
            targetTile.GrowthDays = 0;
            targetTile.IsWatered = false;
            targetTile.IsTilled = true;
            UpdateTileVisual(targetTile);
            _sabotagedCrops++;

            PlayFeedbackClip(_blockedClip, 0.78f);
            SetMessage(UiTextCatalog.Format("stardewai.msg.crop_sabotaged", GetItemLabel(removedCrop)));
        }

        private void BeginNpcDialogue(NpcIdentity npc)
        {
            CancelQueuedMouseAction();
            CancelInventoryDrag();
            CancelInventoryPanelDrag();

            if (npc == NpcIdentity.Qianran && !IsVillageFestivalDay())
            {
                OpenMerchantShop();
                return;
            }

            Vector2 delta = GetNpcWorldPosition(npc) - _playerPosition;
            if (delta.sqrMagnitude > 0.0001f)
            {
                _facing = DominantDirection(delta);
            }

            List<string> lines = BuildNpcDialogueLines(npc);
            if (lines.Count == 0)
            {
                SetMessage(UiTextCatalog.Format("stardewai.msg.npc_greet", GetNpcDisplayName(npc)));
                return;
            }

            _dialogueState.Clear();
            _activeDialogueNpc = npc;
            _dialogueState.Speaker = GetNpcDisplayName(npc);
            _dialogueState.Lines.AddRange(lines);
            _dialogueState.Index = 0;
            _isDialogueOpen = true;
        }

        private void AdvanceDialogue()
        {
            if (!_isDialogueOpen)
            {
                return;
            }

            _dialogueState.Index++;
            if (_dialogueState.Index >= _dialogueState.Lines.Count)
            {
                EndDialogue();
            }
        }

        private void EndDialogue()
        {
            _isDialogueOpen = false;
            _dialogueState.Clear();
            SetMessage(UiTextCatalog.Format("stardewai.msg.npc_chat_done", GetNpcDisplayName(_activeDialogueNpc)));
        }

        private void UpdateDialoguePanel()
        {
            if (_dialoguePanel == null || _dialogueSpeakerText == null || _dialogueBodyText == null)
            {
                return;
            }

            _dialoguePanel.gameObject.SetActive(_isDialogueOpen);
            if (!_isDialogueOpen || _dialogueState.Lines.Count == 0)
            {
                return;
            }

            bool portrait = Screen.height > Screen.width;
            _dialogueSpeakerText.text = _dialogueState.Speaker;
            _dialogueBodyText.text = portrait
                ? _dialogueState.Lines[_dialogueState.Index]
                : _dialogueState.Lines[_dialogueState.Index] + "\n\n<color=#E8D7A7>" + UiTextCatalog.Get("stardewai.dialogue.continue_hint") + "</color>";
        }

        private List<string> BuildNpcDialogueLines(NpcIdentity npc)
        {
            if (IsVillageFestivalDay())
            {
                return BuildFestivalPilotDialogueLines(npc);
            }

            List<string> lines = new List<string>
            {
                GetNpcGreetingLine(npc)
            };

            if (npc == NpcIdentity.XiaoTuanzi)
            {
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.xiaotuanzi.intro1"));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.xiaotuanzi.intro2"));
                lines.Add(GetNpcTimeComment(npc));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.xiaotuanzi.outro"));
                return lines;
            }

            if (npc == NpcIdentity.Qianran)
            {
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.qianran.intro1"));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.qianran.intro2"));
                lines.Add(GetNpcTimeComment(npc));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.qianran.outro"));
                return lines;
            }

            if (npc == NpcIdentity.HaiyinAwa)
            {
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.haiyinawa.intro1"));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.haiyinawa.intro2"));
                lines.Add(GetNpcTimeComment(npc));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.haiyinawa.outro"));
                return lines;
            }

            if (npc == NpcIdentity.Azhai)
            {
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.azhai.intro1"));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.azhai.intro2"));
                lines.Add(GetNpcTimeComment(npc));
                lines.Add(UiTextCatalog.Get("stardewai.dialogue.azhai.outro"));
                return lines;
            }

            switch (_dailyEvent)
            {
                case DailyEventType.NeighborVisit:
                {
                    int giftedSeeds = TryGrantDailyGiftSeeds();
                    if (giftedSeeds > 0)
                    {
                        lines.Add(UiTextCatalog.Format(
                            "stardewai.dialogue.neighbor_visit.gift",
                            giftedSeeds,
                            GetItemLabel(GetSeedPurchaseLineup()[0])));
                    }
                    else if (!_dailyGiftClaimed)
                    {
                        lines.Add(UiTextCatalog.Get("stardewai.dialogue.neighbor_visit.full"));
                    }
                    else
                    {
                        lines.Add(UiTextCatalog.Get("stardewai.dialogue.neighbor_visit.claimed"));
                    }

                    break;
                }
                case DailyEventType.DewMorning:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.dew_morning"));
                    break;
                case DailyEventType.SeedMarket:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.seed_market"));
                    break;
                case DailyEventType.HarvestDay:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.harvest_day"));
                    break;
            }

            lines.Add(GetNpcTimeComment(npc));
            lines.Add(UiTextCatalog.Format("stardewai.dialogue.daily_event", GetDailyEventLabel(_dailyEvent)));
            return lines;
        }

        private List<string> BuildFestivalPilotDialogueLines(NpcIdentity npc)
        {
            List<string> lines = new List<string>();
            switch (npc)
            {
                case NpcIdentity.Lumi:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.lumi.1"));
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.lumi.2"));
                    break;
                case NpcIdentity.XiaoTuanzi:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.xiaotuanzi.1"));
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.xiaotuanzi.2"));
                    break;
                case NpcIdentity.Qianran:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.qianran.1"));
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.qianran.2"));
                    break;
                case NpcIdentity.HaiyinAwa:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.haiyinawa.1"));
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.haiyinawa.2"));
                    break;
                default:
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.azhai.1"));
                    lines.Add(UiTextCatalog.Get("stardewai.dialogue.festival.azhai.2"));
                    break;
            }

            lines.Add(UiTextCatalog.Format("stardewai.dialogue.daily_event", GetDailyEventLabel(_dailyEvent)));
            return lines;
        }

        private string GetNpcGreetingLine(NpcIdentity npc)
        {
            if (npc == NpcIdentity.XiaoTuanzi)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.xiaotuanzi.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.xiaotuanzi.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.xiaotuanzi.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.xiaotuanzi.night");
                    default:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.xiaotuanzi.default");
                }
            }

            if (npc == NpcIdentity.Qianran)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.qianran.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.qianran.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.qianran.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.qianran.night");
                    default:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.qianran.default");
                }
            }

            if (npc == NpcIdentity.HaiyinAwa)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.haiyinawa.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.haiyinawa.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.haiyinawa.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.haiyinawa.night");
                    default:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.haiyinawa.default");
                }
            }

            if (npc == NpcIdentity.Azhai)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.azhai.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.azhai.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.azhai.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.azhai.night");
                    default:
                        return UiTextCatalog.Get("stardewai.dialogue.greeting.azhai.default");
                }
            }

            switch (GetCurrentTimePeriod())
            {
                case TimePeriod.Morning:
                    return UiTextCatalog.Get("stardewai.dialogue.greeting.default.morning");
                case TimePeriod.Noon:
                    return UiTextCatalog.Get("stardewai.dialogue.greeting.default.noon");
                case TimePeriod.Evening:
                    return UiTextCatalog.Get("stardewai.dialogue.greeting.default.evening");
                case TimePeriod.Night:
                    return UiTextCatalog.Get("stardewai.dialogue.greeting.default.night");
                default:
                    return UiTextCatalog.Get("stardewai.dialogue.greeting.default.default");
            }
        }

        private string GetNpcTimeComment(NpcIdentity npc)
        {
            if (npc == NpcIdentity.XiaoTuanzi)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.xiaotuanzi.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.xiaotuanzi.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.xiaotuanzi.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.xiaotuanzi.night");
                    default:
                        return string.Empty;
                }
            }

            if (npc == NpcIdentity.Qianran)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.qianran.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.qianran.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.qianran.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.qianran.night");
                    default:
                        return string.Empty;
                }
            }

            if (npc == NpcIdentity.HaiyinAwa)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.haiyinawa.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.haiyinawa.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.haiyinawa.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.haiyinawa.night");
                    default:
                        return string.Empty;
                }
            }

            if (npc == NpcIdentity.Azhai)
            {
                switch (GetCurrentTimePeriod())
                {
                    case TimePeriod.Morning:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.azhai.morning");
                    case TimePeriod.Noon:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.azhai.noon");
                    case TimePeriod.Evening:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.azhai.evening");
                    case TimePeriod.Night:
                        return UiTextCatalog.Get("stardewai.dialogue.time_comment.azhai.night");
                    default:
                        return string.Empty;
                }
            }

            switch (GetCurrentTimePeriod())
            {
                case TimePeriod.Morning:
                    return UiTextCatalog.Get("stardewai.dialogue.time_comment.default.morning");
                case TimePeriod.Noon:
                    return UiTextCatalog.Get("stardewai.dialogue.time_comment.default.noon");
                case TimePeriod.Evening:
                    return UiTextCatalog.Get("stardewai.dialogue.time_comment.default.evening");
                case TimePeriod.Night:
                    return UiTextCatalog.Get("stardewai.dialogue.time_comment.default.night");
                default:
                    return string.Empty;
            }
        }

        private int TryGrantDailyGiftSeeds()
        {
            if (_dailyGiftClaimed)
            {
                return 0;
            }

            ItemType giftSeed = GetSeedPurchaseLineup()[0];
            int added = AddItem(_backpackSlots, giftSeed, 2);
            if (added > 0)
            {
                _dailyGiftClaimed = true;
            }

            return added;
        }

        private void RefreshCalendarState()
        {
            _calendarDate = _villageCalendar.GetDateForAbsoluteDay(_day);
            _hasFestivalToday = _villageCalendar.TryGetFestival(_calendarDate, out VillageFestival festival);
            _todayFestival = festival;
        }

        private DailyEventType GetDailyEventForDay(int day)
        {
            VillageDate date = _villageCalendar.GetDateForAbsoluteDay(day);
            if (_villageCalendar.TryGetFestival(date, out _))
            {
                return DailyEventType.VillageFestival;
            }

            switch ((day - 1) % 4)
            {
                case 0:
                    return DailyEventType.NeighborVisit;
                case 1:
                    return DailyEventType.DewMorning;
                case 2:
                    return DailyEventType.SeedMarket;
                default:
                    return DailyEventType.HarvestDay;
            }
        }

        private void ApplyDailyEventAtDayStart()
        {
            if (_dailyEvent != DailyEventType.DewMorning)
            {
                return;
            }

            for (int y = 0; y < FieldHeight; y++)
            {
                for (int x = 0; x < FieldWidth; x++)
                {
                    FarmTile tile = _tiles[x, y];
                    if (tile.IsTilled)
                    {
                        tile.IsWatered = true;
                    }
                }
            }
        }

        private int GetCurrentSeedBundleSize()
        {
            return _dailyEvent == DailyEventType.SeedMarket ? SeedBundleSize + 2 : SeedBundleSize;
        }

        private int GetHarvestBonusYield()
        {
            return _dailyEvent == DailyEventType.HarvestDay ? 1 : 0;
        }

        private TimePeriod GetCurrentTimePeriod()
        {
            if (_timeOfDayMinutes < 10f * 60f)
            {
                return TimePeriod.Morning;
            }

            if (_timeOfDayMinutes < 14f * 60f)
            {
                return TimePeriod.Noon;
            }

            if (_timeOfDayMinutes < 18f * 60f)
            {
                return TimePeriod.Evening;
            }

            return TimePeriod.Night;
        }

        private bool IsVillageFestivalDay()
        {
            return _dailyEvent == DailyEventType.VillageFestival;
        }

        private Vector2 GetNpcScheduledPosition(TimePeriod period, NpcIdentity npc)
        {
            if (IsVillageFestivalDay())
            {
                return GetFestivalPilotNpcPosition(npc, period);
            }

            if (npc == NpcIdentity.XiaoTuanzi)
            {
                switch (period)
                {
                    case TimePeriod.Morning:
                        return new Vector2(5.8f, -3.25f);
                    case TimePeriod.Noon:
                        return new Vector2(7.4f, -2.85f);
                    case TimePeriod.Evening:
                        return new Vector2(3.7f, -3.65f);
                    default:
                        return new Vector2(-10.2f, 1.8f);
                }
            }

            if (npc == NpcIdentity.Qianran)
            {
                switch (period)
                {
                    case TimePeriod.Morning:
                        return new Vector2(-6.9f, -3.35f);
                    case TimePeriod.Noon:
                        return new Vector2(-7.8f, -4.1f);
                    case TimePeriod.Evening:
                        return new Vector2(-5.7f, -4.05f);
                    default:
                        return new Vector2(-9.6f, 1.25f);
                }
            }

            if (npc == NpcIdentity.HaiyinAwa)
            {
                switch (period)
                {
                    case TimePeriod.Morning:
                        return new Vector2(8.6f, -0.3f);
                    case TimePeriod.Noon:
                        return new Vector2(7.2f, -1.15f);
                    case TimePeriod.Evening:
                        return new Vector2(5.1f, -3.95f);
                    default:
                        return new Vector2(9.2f, 0.9f);
                }
            }

            if (npc == NpcIdentity.Azhai)
            {
                switch (period)
                {
                    case TimePeriod.Morning:
                        return new Vector2(1.6f, -3.4f);
                    case TimePeriod.Noon:
                        return new Vector2(3.5f, -2.7f);
                    case TimePeriod.Evening:
                        return new Vector2(-0.6f, -4.1f);
                    default:
                        return new Vector2(-10.1f, 2.2f);
                }
            }

            switch (_dailyEvent)
            {
                case DailyEventType.SeedMarket:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                        case TimePeriod.Noon:
                            return new Vector2(-4.6f, -4.05f);
                        case TimePeriod.Evening:
                            return new Vector2(-5.8f, -4.25f);
                        default:
                            return new Vector2(-8.6f, 0.95f);
                    }

                case DailyEventType.HarvestDay:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(-1.7f, -3.55f);
                        case TimePeriod.Noon:
                            return new Vector2(2.0f, -3.85f);
                        case TimePeriod.Evening:
                            return new Vector2(-6.2f, -4.15f);
                        default:
                            return new Vector2(-8.6f, 0.95f);
                    }

                case DailyEventType.VillageFestival:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(-3.1f, -2.9f);
                        case TimePeriod.Noon:
                            return new Vector2(-0.5f, -2.4f);
                        case TimePeriod.Evening:
                            return new Vector2(-4.2f, -3.7f);
                        default:
                            return new Vector2(-8.6f, 0.95f);
                    }

                default:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(-2.2f, -3.85f);
                        case TimePeriod.Noon:
                            return new Vector2(0.8f, -4.35f);
                        case TimePeriod.Evening:
                            return new Vector2(-5.4f, -4.2f);
                        default:
                            return new Vector2(-8.6f, 0.95f);
                    }
            }
        }

        private static Vector2 GetFestivalPilotNpcPosition(NpcIdentity npc, TimePeriod period)
        {
            switch (npc)
            {
                case NpcIdentity.XiaoTuanzi:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(-2.2f, -2.8f);
                        case TimePeriod.Noon:
                            return new Vector2(-1.1f, -2.4f);
                        case TimePeriod.Evening:
                            return new Vector2(-2.9f, -3.2f);
                        default:
                            return new Vector2(-8.9f, 1.5f);
                    }
                case NpcIdentity.Qianran:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(-4.8f, -3.1f);
                        case TimePeriod.Noon:
                            return new Vector2(-3.6f, -2.3f);
                        case TimePeriod.Evening:
                            return new Vector2(-4.4f, -3.4f);
                        default:
                            return new Vector2(-9.3f, 1.3f);
                    }
                case NpcIdentity.HaiyinAwa:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(0.2f, -2.6f);
                        case TimePeriod.Noon:
                            return new Vector2(1.1f, -2.2f);
                        case TimePeriod.Evening:
                            return new Vector2(0.6f, -3.1f);
                        default:
                            return new Vector2(9.0f, 0.8f);
                    }
                case NpcIdentity.Azhai:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(2.4f, -3.0f);
                        case TimePeriod.Noon:
                            return new Vector2(2.9f, -2.6f);
                        case TimePeriod.Evening:
                            return new Vector2(2.1f, -3.5f);
                        default:
                            return new Vector2(-9.8f, 2.0f);
                    }
                default:
                    switch (period)
                    {
                        case TimePeriod.Morning:
                            return new Vector2(-3.1f, -2.9f);
                        case TimePeriod.Noon:
                            return new Vector2(-0.5f, -2.4f);
                        case TimePeriod.Evening:
                            return new Vector2(-4.2f, -3.7f);
                        default:
                            return new Vector2(-8.6f, 0.95f);
                    }
            }
        }

        private Rect GetNpcClickRect(NpcIdentity npc)
        {
            Vector2 position = GetNpcWorldPosition(npc);
            return new Rect(position.x - 0.45f, position.y - 0.05f, 0.9f, 1.2f);
        }

        private bool IsPointNearNpc(Vector2 point, NpcIdentity npc)
        {
            return Vector2.Distance(point, GetNpcWorldPosition(npc)) <= NpcInteractionDistance;
        }

        private Vector2Int GetNpcTalkWalkCell(NpcIdentity npc)
        {
            Vector2 position = GetNpcWorldPosition(npc);
            Vector2Int[] candidates =
            {
                WorldToWalkCell(position + new Vector2(0f, -0.9f)),
                WorldToWalkCell(position + new Vector2(-0.9f, 0f)),
                WorldToWalkCell(position + new Vector2(0.9f, 0f)),
                WorldToWalkCell(position + new Vector2(0f, 0.9f))
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsWalkCellInBounds(candidates[i]) && !_blockedWalkCells.Contains(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return WorldToWalkCell(_playerPosition);
        }

        private Vector2 GetNpcWorldPosition(NpcIdentity npc)
        {
            switch (npc)
            {
                case NpcIdentity.Lumi:
                    return _npcPosition;
                case NpcIdentity.XiaoTuanzi:
                    return _wandererNpcPosition;
                case NpcIdentity.Qianran:
                    return _merchantNpcPosition;
                case NpcIdentity.HaiyinAwa:
                    return _fisherNpcPosition;
                default:
                    return _troubleNpcPosition;
            }
        }

        private string GetNpcDisplayName(NpcIdentity npc)
        {
            switch (npc)
            {
                case NpcIdentity.Lumi:
                    return UiTextCatalog.Get("stardewai.npc.Lumi");
                case NpcIdentity.XiaoTuanzi:
                    return UiTextCatalog.Get("stardewai.npc.XiaoTuanzi");
                case NpcIdentity.Qianran:
                    return UiTextCatalog.Get("stardewai.npc.Qianran");
                case NpcIdentity.HaiyinAwa:
                    return UiTextCatalog.Get("stardewai.npc.HaiyinAwa");
                default:
                    return UiTextCatalog.Get("stardewai.npc.Azhai");
            }
        }

        private bool IsPointNearAnyNpc(Vector2 point)
        {
            return IsPointNearNpc(point, NpcIdentity.Lumi) ||
                IsPointNearNpc(point, NpcIdentity.XiaoTuanzi) ||
                IsPointNearNpc(point, NpcIdentity.Qianran) ||
                IsPointNearNpc(point, NpcIdentity.HaiyinAwa) ||
                IsPointNearNpc(point, NpcIdentity.Azhai);
        }

        private bool TryGetNearbyNpc(Vector2 point, out NpcIdentity npc)
        {
            float lumiDistance = Vector2.Distance(point, _npcPosition);
            float xiaoTuanziDistance = Vector2.Distance(point, _wandererNpcPosition);
            float qianranDistance = Vector2.Distance(point, _merchantNpcPosition);
            float haiyinAwaDistance = Vector2.Distance(point, _fisherNpcPosition);
            float azhaiDistance = Vector2.Distance(point, _troubleNpcPosition);
            float nearest = Mathf.Min(Mathf.Min(lumiDistance, xiaoTuanziDistance), Mathf.Min(qianranDistance, Mathf.Min(haiyinAwaDistance, azhaiDistance)));
            if (nearest > NpcInteractionDistance)
            {
                npc = NpcIdentity.Lumi;
                return false;
            }

            if (lumiDistance <= xiaoTuanziDistance && lumiDistance <= qianranDistance && lumiDistance <= haiyinAwaDistance && lumiDistance <= azhaiDistance)
            {
                npc = NpcIdentity.Lumi;
            }
            else if (xiaoTuanziDistance <= qianranDistance && xiaoTuanziDistance <= haiyinAwaDistance && xiaoTuanziDistance <= azhaiDistance)
            {
                npc = NpcIdentity.XiaoTuanzi;
            }
            else if (qianranDistance <= haiyinAwaDistance && qianranDistance <= azhaiDistance)
            {
                npc = NpcIdentity.Qianran;
            }
            else if (haiyinAwaDistance <= azhaiDistance)
            {
                npc = NpcIdentity.HaiyinAwa;
            }
            else
            {
                npc = NpcIdentity.Azhai;
            }

            return true;
        }

        private bool TryGetNpcUnderPoint(Vector2 point, out NpcIdentity npc)
        {
            bool onLumi = GetNpcClickRect(NpcIdentity.Lumi).Contains(point);
            bool onXiaoTuanzi = GetNpcClickRect(NpcIdentity.XiaoTuanzi).Contains(point);
            bool onQianran = GetNpcClickRect(NpcIdentity.Qianran).Contains(point);
            bool onHaiyinAwa = GetNpcClickRect(NpcIdentity.HaiyinAwa).Contains(point);
            bool onAzhai = GetNpcClickRect(NpcIdentity.Azhai).Contains(point);
            if (!onLumi && !onXiaoTuanzi && !onQianran && !onHaiyinAwa && !onAzhai)
            {
                npc = NpcIdentity.Lumi;
                return false;
            }

            float bestDistance = float.MaxValue;
            npc = NpcIdentity.Lumi;
            if (onLumi)
            {
                bestDistance = Vector2.Distance(point, _npcPosition);
            }

            if (onXiaoTuanzi)
            {
                float distance = Vector2.Distance(point, _wandererNpcPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    npc = NpcIdentity.XiaoTuanzi;
                }
            }

            if (onQianran)
            {
                float distance = Vector2.Distance(point, _merchantNpcPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    npc = NpcIdentity.Qianran;
                }
            }

            if (onHaiyinAwa)
            {
                float distance = Vector2.Distance(point, _fisherNpcPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    npc = NpcIdentity.HaiyinAwa;
                }
            }

            if (onAzhai)
            {
                float distance = Vector2.Distance(point, _troubleNpcPosition);
                if (distance < bestDistance)
                {
                    npc = NpcIdentity.Azhai;
                }
            }

            return true;
        }

        private static string GetTimePeriodLabel(TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Morning:
                    return UiTextCatalog.Get("stardewai.time.morning");
                case TimePeriod.Noon:
                    return UiTextCatalog.Get("stardewai.time.noon");
                case TimePeriod.Evening:
                    return UiTextCatalog.Get("stardewai.time.evening");
                case TimePeriod.Night:
                    return UiTextCatalog.Get("stardewai.time.night");
                default:
                    return UiTextCatalog.Get("stardewai.time.unknown");
            }
        }

        private string GetDailyEventLabel(DailyEventType dailyEvent)
        {
            switch (dailyEvent)
            {
                case DailyEventType.NeighborVisit:
                    return UiTextCatalog.Get("stardewai.event.neighbor_visit");
                case DailyEventType.DewMorning:
                    return UiTextCatalog.Get("stardewai.event.dew_morning");
                case DailyEventType.SeedMarket:
                    return UiTextCatalog.Get("stardewai.event.seed_market");
                case DailyEventType.HarvestDay:
                    return UiTextCatalog.Get("stardewai.event.harvest_day");
                default:
                    return UiTextCatalog.Get("stardewai.event.none");
            }
        }

        private static string FormatTime(float totalMinutes)
        {
            int minutes = Mathf.Clamp(Mathf.FloorToInt(totalMinutes), 0, Mathf.FloorToInt(DayEndMinutes));
            int hour = minutes / 60;
            int minute = minutes % 60;
            return hour.ToString("00") + ":" + minute.ToString("00");
        }

        private void UseActiveTool()
        {
            CancelQueuedMouseAction();

            Vector2 frontPoint = _playerPosition + (Vector2)_facing;
            if (TryGetNearbyNpc(frontPoint, out NpcIdentity nearbyNpc))
            {
                _queuedTalkNpc = nearbyNpc;
                BeginNpcDialogue(nearbyNpc);
                return;
            }

            if (IsPointNearRect(frontPoint, _shippingBinClickRect))
            {
                ShipBackpackCrops();
                return;
            }

            if (IsPointNearRect(frontPoint, _seedChestClickRect))
            {
                BuySeedBundle();
                return;
            }

            UseToolOnGrid(GetTargetGrid(), _activeTool);
        }

        private void UseToolOnGrid(Vector2Int grid)
        {
            UseToolOnGrid(grid, _activeTool);
        }

        private void UseToolOnGrid(Vector2Int grid, ToolType tool)
        {
            if (!TryGetTile(grid, out FarmTile tile))
            {
                SetMessage(UiTextCatalog.Get("stardewai.msg.out_of_field"));
                return;
            }

            TriggerToolActionAnimation(tool);
            bool didHit = false;

            switch (tool)
            {
                case ToolType.Hoe:
                    if (tile.HasCrop)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.crop_exists"));
                        break;
                    }

                    if (tile.IsTilled)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.tilled_already"));
                        break;
                    }

                    tile.IsTilled = true;
                    tile.IsWatered = false;
                    didHit = true;
                    SetMessage(UiTextCatalog.Get("stardewai.msg.hoed"));
                    break;

                case ToolType.WateringCan:
                    if (!tile.IsTilled)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.need_till_first"));
                        break;
                    }

                    tile.IsWatered = true;
                    didHit = true;
                    SetMessage(UiTextCatalog.Get(tile.HasCrop ? "stardewai.msg.already_watered_crop" : "stardewai.msg.already_watered_tile"));
                    break;

                case ToolType.Seeds:
                    if (!tile.IsTilled)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.need_till_first"));
                        break;
                    }

                    if (tile.HasCrop)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.crop_exists"));
                        break;
                    }

                    ItemType seedItem = GetPreferredSeedItemForPlanting();
                    if (seedItem == ItemType.None)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.no_seed_selected"));
                        break;
                    }

                    RemoveItem(_backpackSlots, seedItem, 1);
                    tile.HasCrop = true;
                    tile.CropItemType = GetCropFromSeedItem(seedItem);
                    tile.GrowthDays = 0;
                    didHit = true;
                    SetMessage(UiTextCatalog.Format("stardewai.msg.planted", GetItemLabel(seedItem)));
                    break;

                case ToolType.Harvest:
                    if (!tile.HasCrop)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.nothing_harvest"));
                        break;
                    }

                    ItemType cropItem = tile.CropItemType;
                    if (!IsCropItem(cropItem))
                    {
                        cropItem = ItemType.Parsnip;
                    }

                    if (!IsCropRipe(tile))
                    {
                        SetMessage(UiTextCatalog.Format("stardewai.msg.crop_growing", GetItemLabel(cropItem), GetCropDaysToRipen(cropItem) - tile.GrowthDays));
                        break;
                    }

                    int harvestYield = 1 + GetHarvestBonusYield();
                    int harvestedAmount = AddItem(_backpackSlots, cropItem, harvestYield);
                    if (harvestedAmount <= 0)
                    {
                        SetMessage(UiTextCatalog.Get("stardewai.msg.no_backpack_space_harvest"));
                        break;
                    }

                    tile.HasCrop = false;
                    tile.CropItemType = ItemType.None;
                    tile.GrowthDays = 0;
                    tile.IsWatered = false;
                    tile.IsTilled = true;
                    _harvestedCrops += harvestedAmount;
                    didHit = true;
                    if (harvestedAmount > 1)
                    {
                        SetMessage(UiTextCatalog.Format("stardewai.msg.harvested_bonus", harvestedAmount, GetItemLabel(cropItem)));
                    }
                    else
                    {
                        SetMessage(UiTextCatalog.Format("stardewai.msg.harvested", harvestedAmount, GetItemLabel(cropItem)));
                    }
                    break;
            }

            UpdateTileVisual(tile);
            if (didHit)
            {
                PlayFeedbackClip(GetToolHitClip(tool), GetToolHitVolume(tool));
                TriggerToolHitEffect(grid, tool);
            }
        }

        private void AdvanceDay(bool autoTriggered = false)
        {
            _isDialogueOpen = false;
            _dialogueState.Clear();
            _isMerchantShopOpen = false;
            if (_merchantShopPanel != null)
            {
                _merchantShopPanel.gameObject.SetActive(false);
            }
            _day++;
            RefreshCalendarState();
            int progressed = 0;
            _lastShipmentGold = GetContainerSellValue(_shippingSlots);
            if (_lastShipmentGold > 0)
            {
                _gold += _lastShipmentGold;
                ClearContainer(_shippingSlots);
            }

            for (int y = 0; y < FieldHeight; y++)
            {
                for (int x = 0; x < FieldWidth; x++)
                {
                    FarmTile tile = _tiles[x, y];
                    int daysToRipen = GetCropDaysToRipen(tile.CropItemType);
                    if (tile.HasCrop && tile.IsWatered && tile.GrowthDays < daysToRipen)
                    {
                        tile.GrowthDays++;
                        progressed++;
                    }

                    tile.IsWatered = false;
                }
            }

            _dailyEvent = GetDailyEventForDay(_day);
            _dailyGiftClaimed = false;
            _timeOfDayMinutes = DayStartMinutes;
            _lastTimePeriod = GetCurrentTimePeriod();
            ApplyDailyEventAtDayStart();
            _npcTargetPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.Lumi);
            _wandererNpcTargetPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.XiaoTuanzi);
            _merchantNpcTargetPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.Qianran);
            _fisherNpcTargetPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.HaiyinAwa);
            _troubleNpcTargetPosition = GetNpcScheduledPosition(_lastTimePeriod, NpcIdentity.Azhai);
            _cropSabotageTimer = UnityEngine.Random.Range(10f, 18f);

            UpdateAllTileVisuals();

            string settlement = _lastShipmentGold > 0
                ? UiTextCatalog.Format("stardewai.day.settlement.income", _lastShipmentGold)
                : UiTextCatalog.Get("stardewai.day.settlement.none");
            string prefix = autoTriggered ? UiTextCatalog.Get("stardewai.day.auto_rest") : string.Empty;
            string eventLabel = UiTextCatalog.Format("stardewai.day.event_label", GetDailyEventLabel(_dailyEvent));

            if (progressed > 0)
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.day.start.grown",
                    prefix,
                    _day,
                    settlement,
                    progressed,
                    eventLabel));
            }
            else
            {
                SetMessage(UiTextCatalog.Format(
                    "stardewai.day.start.water",
                    prefix,
                    _day,
                    settlement,
                    eventLabel));
            }
        }

        private void UpdateAllTileVisuals()
        {
            for (int y = 0; y < FieldHeight; y++)
            {
                for (int x = 0; x < FieldWidth; x++)
                {
                    UpdateTileVisual(_tiles[x, y]);
                }
            }
        }

        private void UpdateTileVisual(FarmTile tile)
        {
            TileBase soilTile = null;
            if (tile.IsTilled)
            {
                soilTile = tile.IsWatered
                    ? FarmPixelArtFactory.GetTile(FarmTileArt.SoilWet)
                    : FarmPixelArtFactory.GetTile(FarmTileArt.SoilDry);
            }

            _fieldTilemap.SetTile(tile.Cell, soilTile);

            TileBase cropTile = null;
            if (tile.HasCrop)
            {
                if (tile.GrowthDays <= 0)
                {
                    cropTile = FarmPixelArtFactory.GetTile(FarmTileArt.CropSeed);
                }
                else if (tile.GrowthDays == 1)
                {
                    cropTile = FarmPixelArtFactory.GetTile(FarmTileArt.CropSprout);
                }
                else if (tile.GrowthDays == 2)
                {
                    cropTile = FarmPixelArtFactory.GetTile(FarmTileArt.CropLeafy);
                }
                else
                {
                    cropTile = FarmPixelArtFactory.GetTile(FarmTileArt.CropRipe);
                }
            }

            _cropTilemap.SetTile(tile.Cell, cropTile);
        }

    }
}

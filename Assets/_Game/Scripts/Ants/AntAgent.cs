using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace ColonyFlow
{
    public sealed class AntAgent : GameUnit
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.4f;
        [SerializeField, Min(0f)] private float attackDuration = 0.32f;
        [SerializeField, Range(0.5f, 3f)] private float sizeMultiplier = 1.28f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private AntVisual antVisual;

        private readonly List<Vector3> route = new();
        private readonly List<Vector3> returnRoute = new();
        private PixelBoard board;
        private PixelCell target;
        private PixelCellView carriedBox;
        private Action<AntAgent, PixelCell, bool> completed;
        private int waypointIndex;
        private int returnWaypointIndex;
        private float attackTimer;
        private bool pickedUp;
        private int returnRouteIndex;
        private Vector3 travelScale;
        private Transform spriteAntRoot;
        private SpriteRenderer visibleAntRenderer;
        private bool isJumpingIntoHole;

        public AntState State { get; private set; } = AntState.Inactive;
        public PixelCell Target => target;

        private void Awake()
        {
            if (spriteRenderer == null) return;
            if (spriteRenderer.sprite == null) spriteRenderer.sprite = RuntimeSprite.Square;
            spriteRenderer.sortingOrder = 20;
        }

        public void Configure(SpriteRenderer renderer)
        {
            spriteRenderer = renderer;
            if (spriteRenderer.sprite == null) spriteRenderer.sprite = RuntimeSprite.Square;
            spriteRenderer.sortingOrder = 20;
        }

        public void Configure(AntVisual visual)
        {
            antVisual = visual;
            spriteRenderer = null;
        }

        public void Launch(PixelBoard pixelBoard, PixelCell pixel, IReadOnlyList<Vector3> waypoints,
            int returnDestinationIndex, IReadOnlyList<Vector3> returnWaypoints, Color color,
            Action<AntAgent, PixelCell, bool> onCompleted)
        {
            TF.DOKill();
            isJumpingIntoHole = false;
            board = pixelBoard;
            target = pixel;
            ReleaseCarriedBox();
            completed = onCompleted;
            route.Clear();
            for (int i = 0; i < waypoints.Count; i++)
                route.Add(pixelBoard.ProjectToGameplayPlane(waypoints[i]));
            returnRoute.Clear();
            if (returnWaypoints != null)
                for (int i = 0; i < returnWaypoints.Count; i++)
                    returnRoute.Add(pixelBoard.ProjectToGameplayPlane(returnWaypoints[i]));
            waypointIndex = 0;
            returnWaypointIndex = Mathf.Clamp(returnDestinationIndex, 0, Mathf.Max(0, route.Count - 1));
            returnRouteIndex = 0;
            attackTimer = attackDuration;
            pickedUp = false;
            if (antVisual != null) antVisual.SetColor(color);
            else if (spriteRenderer != null) spriteRenderer.color = color;
            EnsureVisibleSpriteAnt(color);
            // Keep the ant readable as a polished character at phone resolution.
            travelScale = Vector3.one * (board.CellSize * sizeMultiplier);
            TF.localScale = travelScale;
            if (route.Count > 0) TF.position = route[0];
            State = route.Count > 1 ? AntState.OnBorder : AntState.Attacking;
            if (State == AntState.Attacking) antVisual?.PlayAttack();
            else antVisual?.PlayWalk();
        }

        private void Update()
        {
            if (State == AntState.Inactive || State == AntState.Completed || isJumpingIntoHole) return;

            if (State != AntState.Returning && !pickedUp && (target == null || target.IsDestroyed))
            {
                Finish(false);
                return;
            }

            if (State == AntState.Returning)
            {
                if (waypointIndex >= returnWaypointIndex)
                {
                    Vector3 prev = route[waypointIndex];
                    FaceTravelDirection(prev - TF.position);
                    TF.position = Vector3.MoveTowards(TF.position, prev, moveSpeed * Time.deltaTime);
                    if ((TF.position - prev).sqrMagnitude <= 0.000001f) waypointIndex--;
                }
                else
                {
                    if (returnRoute.Count == 0 || returnRouteIndex >= returnRoute.Count)
                    {
                        Finish(true);
                        return;
                    }

                    Vector3 entrance = returnRoute[returnRoute.Count - 1];
                    float distanceToHole = Vector3.Distance(TF.position, entrance);
                    
                    if (distanceToHole <= board.CellSize * 2.5f)
                    {
                        isJumpingIntoHole = true;
                        TF.DOJump(entrance, 0.7f, 1, 0.35f).SetEase(Ease.InOutSine);
                        TF.DOScale(0f, 0.35f).SetEase(Ease.InBack).OnComplete(() => Finish(true));
                        return;
                    }

                    Vector3 destination = returnRoute[returnRouteIndex];
                    FaceTravelDirection(destination - TF.position);
                    TF.position = Vector3.MoveTowards(TF.position, destination, moveSpeed * Time.deltaTime);
                    if ((TF.position - destination).sqrMagnitude <= 0.000001f)
                    {
                        returnRouteIndex++;
                        if (returnRouteIndex >= returnRoute.Count) Finish(true);
                    }
                }
                return;
            }

            if (waypointIndex < route.Count - 1)
            {
                Vector3 next = route[waypointIndex + 1];
                FaceTravelDirection(next - TF.position);
                TF.position = Vector3.MoveTowards(TF.position, next, moveSpeed * Time.deltaTime);
                if ((TF.position - next).sqrMagnitude <= 0.000001f) waypointIndex++;
                return;
            }

            if (State != AntState.Attacking)
            {
                State = AntState.Attacking;
                antVisual?.PlayAttack();
            }
            attackTimer -= Time.deltaTime;
            // Pick the cube up after the head has dipped, then leave enough time for
            // the lift to finish before the ant turns back toward the nest.
            if (!pickedUp && attackTimer <= attackDuration * 0.55f)
            {
                if (board.TryCollectPixel(target, this, out carriedBox))
                {
                    if (carriedBox != null)
                    {
                        carriedBox.BeginCarry(TF, board.CellSize);
                        Color pixelColor = visibleAntRenderer != null ? visibleAntRenderer.color : Color.white;
                        PixelPickupFx.Ins.PlayAt(carriedBox.TF.position, pixelColor);
                        SoundManager.Ins?.PlayGameFx(GameFxID.BoxCollected);
                    }
                    pickedUp = true;
                }
                else
                {
                    Finish(false);
                }
            }

            if (pickedUp && attackTimer <= 0f)
            {
                State = AntState.Returning;
                antVisual?.PlayWalk();
                waypointIndex = route.Count - 2;
            }
        }

        private void Finish(bool success)
        {
            if (target != null && !success) target.Release(this);
            ReleaseCarriedBox();
            State = AntState.Completed;
            antVisual?.PlayIdle();
            Action<AntAgent, PixelCell, bool> callback = completed;
            PixelCell finishedTarget = target;
            completed = null;
            target = null;
            route.Clear();
            returnRoute.Clear();
            callback?.Invoke(this, finishedTarget, success);
        }

        private void OnDisable()
        {
            if (target != null) target.Release(this);
            ReleaseCarriedBox();
            target = null;
            completed = null;
            route.Clear();
            returnRoute.Clear();
            State = AntState.Inactive;
            TF.localScale = Vector3.one;
            if (spriteAntRoot != null) spriteAntRoot.localRotation = Quaternion.identity;
        }

        private void ReleaseCarriedBox()
        {
            if (carriedBox == null) return;
            carriedBox.ReturnToPool();
            carriedBox = null;
        }

        private void EnsureVisibleSpriteAnt(Color colonyColor)
        {
            // The authored AntChibi prefab is a real shaded 3D ant. Its children
            // must follow the gameplay render layer because Unity does not inherit
            // a parent's layer when pooled objects are spawned.
            if (antVisual != null)
            {
                int gameplayLayer = transform.parent != null ? transform.parent.gameObject.layer : gameObject.layer;
                SetLayerRecursively(transform, gameplayLayer);
                antVisual.SetVisible(true);
                if (spriteRenderer != null) spriteRenderer.enabled = false;
                if (spriteAntRoot != null) spriteAntRoot.gameObject.SetActive(false);
                return;
            }
            if (spriteRenderer != null) spriteRenderer.enabled = false;
            if (spriteAntRoot == null)
            {
                spriteAntRoot = new GameObject("VisibleAntSprite").transform;
                spriteAntRoot.SetParent(transform, false);
                spriteAntRoot.localPosition = new Vector3(0f, 0f, -.22f);
                visibleAntRenderer = spriteAntRoot.gameObject.AddComponent<SpriteRenderer>();
                visibleAntRenderer.sprite = RuntimeSprite.Ant;
                visibleAntRenderer.material = RuntimeSprite.MaterialFor(visibleAntRenderer.sprite);
                visibleAntRenderer.sortingOrder = 55;
            }
            visibleAntRenderer.color = Color.Lerp(Color.white, colonyColor, .10f);
            spriteAntRoot.gameObject.SetActive(true);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayerRecursively(child, layer);
        }

        private void FaceTravelDirection(Vector3 direction)
        {
            antVisual?.FaceDirection(direction);
            if (spriteAntRoot == null || direction.sqrMagnitude < .000001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            spriteAntRoot.localRotation = Quaternion.Lerp(spriteAntRoot.localRotation, targetRotation, 15f * Time.deltaTime);
        }
    }
}

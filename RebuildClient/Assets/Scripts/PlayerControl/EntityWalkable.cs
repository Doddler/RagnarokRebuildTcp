using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.MapEditor;
using UnityEngine;

namespace Assets.Scripts
{
    class EntityWalkable : MonoBehaviour
    {
        private RoWalkDataProvider walkProvider;

        public List<Vector2Int> MovePath;
        public float MoveSpeed = 0.2f;
        public float MoveProgress;
        public Vector3 StartPos;

        private Vector2Int[] tempPath;

        public bool IsWalking => MovePath != null && MovePath.Count > 1;
        public FacingDirection WalkFacing => GetDirectionForOffset(MovePath[1] - MovePath[0]);

        public Vector2Int GetCurrentPosition(Vector3 worldPos)
        {
            if (IsWalking)
                return MovePath[0];
            return walkProvider.GetTilePositionForPoint(worldPos);
        }

        private FacingDirection GetDirectionForOffset(Vector2Int offset)
        {

            if (offset.x == -1 && offset.y == -1) return FacingDirection.SouthWest;
            if (offset.x == -1 && offset.y == 0) return FacingDirection.West;
            if (offset.x == -1 && offset.y == 1) return FacingDirection.NorthWest;
            if (offset.x == 0 && offset.y == 1) return FacingDirection.North;
            if (offset.x == 1 && offset.y == 1) return FacingDirection.NorthEast;
            if (offset.x == 1 && offset.y == 0) return FacingDirection.East;
            if (offset.x == 1 && offset.y == -1) return FacingDirection.SouthEast;
            if (offset.x == 0 && offset.y == -1) return FacingDirection.South;

            return FacingDirection.South;
        }

        private bool IsDiagonal(FacingDirection dir)
        {
            if (dir == FacingDirection.NorthEast || dir == FacingDirection.NorthWest ||
                dir == FacingDirection.SouthEast || dir == FacingDirection.SouthWest)
                return true;
            return false;
        }

        private bool IsNeighbor(Vector2Int pos1, Vector2Int pos2)
        {
            var x = Mathf.Abs(pos1.x - pos2.x);
            var y = Mathf.Abs(pos1.y - pos2.y);

            if (x <= 1 && y <= 1)
                return true;
            return false;
        }

        public void Awake()
        {
            walkProvider = RoWalkDataProvider.Instance;
            var tile = walkProvider.GetTilePositionForPoint(transform.position);
            var start = new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            transform.position = new Vector3(start.x, walkProvider.GetHeightForPosition(start), start.z);
            MovePath = new List<Vector2Int>();
        }

        public bool BeginMove(Vector2Int target)
        {
            if (Mathf.Approximately(MoveSpeed, 0))
                return false; //can't move
            var start = walkProvider.GetTilePositionForPoint(transform.position);

            if (MovePath.Count >= 1)
                start = MovePath[0];

            var end = target;
            
            if (MovePath.Count == 0 || MovePath.Last() != end)
            {
                if(tempPath == null)
                    tempPath = new Vector2Int[17];

                //Debug.Log(TempPath.Length);

                var steps = Pathfinder.GetPath(walkProvider.WalkData, start, end, tempPath);

                if (steps > 0)
                {
                    if (MovePath.Count > 1)
                    {
                        //Debug.Log($"{MovePath[0]} {tempPath[0]} {MovePath[1]} {tempPath[1]}");

                        if (!IsNeighbor(MovePath[1], tempPath[1]))
                            MoveProgress = 1f;
                        else
                        {
                            if(MovePath[1] != tempPath[1])
                                MoveProgress = Mathf.Min(1f, MoveProgress + 0.3f);
                        }
                    }
                    else
                        MoveProgress = 1f;

                    MovePath.Clear();

                    for(var i = 0; i < steps; i++)
                        MovePath.Add(tempPath[i]);
                    //MovePath = path;
                    //var str = string.Join(", ", path);

                    var offset = MovePath[1] - MovePath[0];
                    //if (IsDiagonal(GetDirectionForOffset(offset)))
                    //    MoveProgress += 0.3f;
                    StartPos = transform.position - new Vector3(0.5f, 0f, 0.5f);

                    return true;
                }
            }

            return false;
        }

        public bool BeginMove(Vector3 target)
        {
            var t = walkProvider.GetTilePositionForPoint(target);

            return BeginMove(t);
        }

        private void UpdateMovePath()
        {
            //if (MovePath != null && MovePath.Count == 0)
            //    MovePath = null;

            if (MovePath.Count != 0)
            {
                if (MovePath.Count > 1)
                {

                    var offset = MovePath[1] - MovePath[0];
                    if (IsDiagonal(GetDirectionForOffset(offset)))
                        MoveProgress -= Time.deltaTime / MoveSpeed * 0.80f;
                    else
                        MoveProgress -= Time.deltaTime / MoveSpeed;
                }

                while (MoveProgress < 0f && MovePath.Count > 1)
                {
                    MovePath.RemoveAt(0);
                    StartPos = new Vector3(MovePath[0].x, walkProvider.GetHeightForPosition(transform.position), MovePath[0].y);
                    MoveProgress += 1f;
                }

                if (MovePath.Count == 0)
                    Debug.Log("WAAA");

                if (MovePath.Count == 1)
                {
                    var last = MovePath[0];
                    transform.position = new Vector3(last.x + 0.5f, walkProvider.GetHeightForPosition(transform.position), last.y + 0.5f);
                    MovePath.Clear();
                }
                else
                {
                    var xPos = Mathf.Lerp(StartPos.x, MovePath[1].x, 1 - MoveProgress);
                    var yPos = Mathf.Lerp(StartPos.z, MovePath[1].y, 1 - MoveProgress);

                    transform.position = new Vector3(xPos + 0.5f, walkProvider.GetHeightForPosition(transform.position), yPos + 0.5f);

                    var offset = MovePath[1] - MovePath[0];
                }
            }
        }

        public void Update()
        {
            UpdateMovePath();

            //var targetHeight = Mathf.Lerp(transform.position.y, walkProvider.GetHeightForPosition(transform.position), Time.deltaTime * 20f);
            //transform.position = new Vector3(transform.position.x, targetHeight, transform.position.z);

        }
    }
}

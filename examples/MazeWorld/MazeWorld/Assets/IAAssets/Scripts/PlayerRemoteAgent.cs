﻿using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Net;
using System.Text;
using UnityStandardAssets.Characters.ThirdPerson;
using UnityEngine.SceneManagement;
using unityremote;

namespace unityremote
{

    public class PlayerRemoteAgent : Agent
    {


        //BEGIN::Game controller variables
        private ThirdPersonCharacter character;
        private Transform m_CamTransform;
        private Vector3 m_CamForward;             // The current forward direction of the camera
        private Vector3 m_Move;
        //END::

        //BEGIN::motor controll variables
        private static float fx, fy;
        private float speed = 0.0f;
        private bool crouch;
        private bool jump;
        private float leftTurn = 0;
        private float rightTurn = 0;
        private float up = 0;
        private float down = 0;
        private bool pushing;
        private bool getpickup;
        private bool walkspeed;
        private UdpClient socket;
        private bool commandReceived;
        public int rayCastingWidth;
        public int rayCastingHeight;
        //END::


        public Text hud;

        private GameObject player;
        
        private PlayerRemoteSensor sensor;

        public Camera m_camera;

        private Rigidbody mRigidBody;

        public float initialEnergy = 30;

        private float energy;

        public float energyRatio = 1.0f;

        private int touchID = 0;

        private bool done;

        private bool isNewState = false;

        public GameObject TopLeftCorner, BottonRightCorner;

        public GameObject[] respawnPositions;

        private float reward = 1;

        public GameObject restartButton;

        // Use this for initialization
        void Start()
        {
            
            if (restartButton != null) {
                Button btn = restartButton.GetComponent<Button>();
		        btn.onClick.AddListener(OnClick);
            }

            mRigidBody = GetComponent<Rigidbody>();
            commandReceived = false;
            Restart();
            if (!gameObject.activeSelf)
            {
                return;
            }
            player = GameObject.FindGameObjectsWithTag("Player")[0];

            if (m_camera != null)
            {
                m_CamTransform = m_camera.transform;
            }
            else
            {
                Debug.LogWarning(
                    "Warning: no main camera found. Third person character needs a Camera tagged \"MainCamera\", for camera-relative controls.", gameObject);
                // we use self-relative controls in this case, which probably isn't what the user wants, but hey, we warned them!
            }

            // get the third person character ( this should never be null due to require component )
            character = GetComponent<ThirdPersonCharacter>();
            sensor = new PlayerRemoteSensor();
            sensor.Start(m_camera, player, this.rayCastingHeight, this.rayCastingWidth);
        }
        
        private float deltaTime = 0;

        private void Restart(){
            deltaTime = 0;
            energy = initialEnergy;
            ResetState();
            done = false;
            isNewState = true;
            int idx = Random.Range(0, respawnPositions.Length);
            //mRigidBody.position = respawnPositions[idx].transform.position;

            Vector3 pos = respawnPositions[idx].transform.position;
            mRigidBody.MovePosition(pos);
        }

        private void ResetState()
        {
            speed = 0.0f;
            fx = 0;
            fy = 0;
            crouch = false;
            jump = false;
            pushing = false;
            leftTurn = 0;
            rightTurn = 0;
            up = 0;
            down = 0;
            touchID = 0;
            reward = 0;
        }


        private void UpdateHUD(){
            if (hud != null) {
                hud.text = "Energy: " + System.Math.Round(energy,2) + "\tReward: " + reward + "\tDone: " + done;
            }
        }

        public override void ApplyAction()
        {
            isNewState = true;
            string action = GetActionName();
            if (action=="restart") {
                Restart();
            } else if (!done) {
                ResetState();
                switch (action)
                {
                    case "fx":
                        fx = GetActionArgAsFloat();
                        break;
                    case "fy":
                        fy = GetActionArgAsFloat();
                        break;
                    case "left_turn":
                        leftTurn = GetActionArgAsFloat();
                        break;
                    case "right turn":
                        rightTurn = GetActionArgAsFloat();
                        break;
                    case "up":
                        up = GetActionArgAsFloat();
                        break;
                    case "down":
                        down = GetActionArgAsFloat();
                        break;
                    case "push":
                        pushing = GetActionArgAsBool();
                        break;
                    case "jump":
                        jump = GetActionArgAsBool();
                        break;
                    case "crouch":
                        crouch = GetActionArgAsBool();
                        break;
                    case "pickup":
                        getpickup = GetActionArgAsBool();
                        break;
                }
            }
        }


        // Update is called once per frame
        public override void UpdatePhysics()
        {

            deltaTime += Time.deltaTime;
            if (deltaTime > 1.0){
                energy -= energyRatio;
                if (energy < 0){
                    energy = 0;
                }
                deltaTime = 0;
            }

            // read inputs
            float h = fx;
            float v = fy;


            // calculate move direction to pass to character
            if (m_CamTransform != null)
            {

                // calculate camera relative direction to move:
                m_CamForward = Vector3.Scale(m_CamTransform.forward, new Vector3(1, 0, 1)).normalized;
                m_Move = v * m_CamForward + h * m_CamTransform.right;

            }
            else
            {
                // we use world-relative directions in the case of no main camera
                m_Move = v * Vector3.forward + h * Vector3.right;
            }


            // walk speed multiplier
            if (walkspeed) {
                m_Move *= speed;
            } 

            // pass all parameters to the character control script
            character.Move(m_Move, crouch, jump, rightTurn - leftTurn, down - up, pushing, fx, fy, getpickup);
            //character.Move(m_Move, crouch, m_Jump, h, v, pushing);
            jump = false;
            sensor.UpdateViewMatrix();
            float x = transform.localPosition.x;
            float z = transform.localPosition.z;
            float tx = TopLeftCorner.transform.localPosition.x;
            float bx = BottonRightCorner.transform.localPosition.x;
            float tz = TopLeftCorner.transform.localPosition.z;
            float bz = BottonRightCorner.transform.localPosition.z;
            if (x < tx || x > bx || z > tz || z < bz) {
                done = true;
                reward = 1;
            }
        }

        /// <summary>
        /// OnCollisionEnter is called when this collider/rigidbody has begun
        /// touching another rigidbody/collider.
        /// </summary>
        /// <param name="other">The Collision data associated with this collision.</param>
        void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.tag == "Fire"){
                energy -= 10;
                touchID = -1;
            } else if (other.gameObject.tag == "Life"){
                touchID = 1;
                energy += 10;
            }
        }

        public void OnClick(){
            Restart();
        }

        public override void UpdateState()
        {
            if (energy <= 0) {
                energy = 0;
                done = true;
            }

            SetStateAsString(0, "frame", sensor.getCurrentRayCastingFrame());
            SetStateAsFloat(1, "reward", reward);
            SetStateAsFloat(2, "touchID", touchID);
            SetStateAsFloat(3, "energy", energy);
            SetStateAsBool(4, "done", done);
            SetStateAsBool(5, "isNewState", isNewState);
            if (isNewState) {
                UpdateHUD();
                isNewState = false;
            }
            if(done){
                ResetState();
            }
        }
    }

    public class PlayerRemoteSensor
    {
        private byte[] currentFrame;
        
        private Camera m_camera;

        private GameObject player;

        private int life, score;
        private float energy;


        private int verticalResolution = 20;
        private int horizontalResolution = 20;
        private bool useRaycast = true;

        private Ray[,] raysMatrix = null;
        private int[,] viewMatrix = null;
        private Vector3 fw1 = new Vector3(), fw2 = new Vector3(), fw3 = new Vector3();

        
        public void SetCurrentFrame(byte[] cf)
        {
            this.currentFrame = cf;
        }

        // Use this for initialization
        public void Start(Camera cam, GameObject player, int rayCastingHRes, int rayCastingVRes)
        {
            this.verticalResolution = rayCastingVRes;
            this.horizontalResolution = rayCastingHRes;
            life = 0;
            score = 0;
            energy = 0;
            useRaycast = true;
            currentFrame = null;

            m_camera = cam;
            this.player = player;
            fw3 = m_camera.transform.forward;


            if (useRaycast)
            {
                if (raysMatrix == null)
                {
                    raysMatrix = new Ray[verticalResolution, horizontalResolution];
                }
                if (viewMatrix == null)
                {
                    viewMatrix = new int[verticalResolution, horizontalResolution];

                }
                for (int i = 0; i < verticalResolution; i++)
                {
                    for (int j = 0; j < horizontalResolution; j++)
                    {
                        raysMatrix[i, j] = new Ray();
                    }
                }
                currentFrame = updateCurrentRayCastingFrame();
            }    
        }



        public byte[] updateCurrentRayCastingFrame()
        {
            UpdateRaysMatrix(m_camera.transform.localPosition, m_camera.transform.forward, m_camera.transform.up, m_camera.transform.right);
            UpdateViewMatrix();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < verticalResolution; i++)
            {
                for (int j = 0; j < horizontalResolution; j++)
                {
                    sb.Append(viewMatrix[i, j]).Append(",");
                }
                sb.Append(";");
            }
            return Encoding.UTF8.GetBytes(sb.ToString().ToCharArray());
        }


        public string getCurrentRayCastingFrame()
        {
            UpdateRaysMatrix(m_camera.transform.localPosition, m_camera.transform.forward, m_camera.transform.up, m_camera.transform.right);
            UpdateViewMatrix();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < verticalResolution; i++)
            {
                for (int j = 0; j < horizontalResolution; j++)
                {
                    sb.Append(viewMatrix[i, j]);
                    if (j < horizontalResolution-1){
                        sb.Append(",");
                    }
                }
                if (i < verticalResolution-1){
                    sb.Append(";");
                }
            }
            return sb.ToString();
        }

        private void UpdateRaysMatrix(Vector3 position, Vector3 forward, Vector3 up, Vector3 right, float fieldOfView = 45.0f)
        {


            float vangle = 2 * fieldOfView / verticalResolution;
            float hangle = 2 * fieldOfView / horizontalResolution;

            float ivangle = -fieldOfView;

            for (int i = 0; i < verticalResolution; i++)
            {
                float ihangle = -fieldOfView;
                fw1 = (Quaternion.AngleAxis(ivangle + vangle * i, right) * forward).normalized;
                fw2.Set(fw1.x, fw1.y, fw1.z);

                for (int j = 0; j < horizontalResolution; j++)
                {
                    raysMatrix[i, j].origin = position;
                    raysMatrix[i, j].direction = (Quaternion.AngleAxis(ihangle + hangle * j, up) * fw2).normalized;
                }
            }
        }

        public void UpdateViewMatrix(float maxDistance = 500.0f)
        {
            for (int i = 0; i < verticalResolution; i++)
            {
                for (int j = 0; j < horizontalResolution; j++)
                {
                    RaycastHit hitinfo;
                    if (Physics.Raycast(raysMatrix[i, j], out hitinfo, maxDistance))
                    {
                        string objname = hitinfo.collider.gameObject.name;
                        switch (objname)
                        {
                            case "Terrain":
                                viewMatrix[i, j] = 1;
                                break;
                            case "maze":
                                viewMatrix[i, j] = 2;
                                break;
                            default:
                                objname = hitinfo.collider.gameObject.tag;
                                if (objname == "Fire")
                                {
                                    viewMatrix[i, j] = -3;
                                }
                                else if (objname=="Life")
                                {
                                    viewMatrix[i, j] = 3;
                                }
                                else
                                {
                                    viewMatrix[i, j] = 0;
                                }
                                break;
                        }
                    }
                    else
                    {
                        viewMatrix[i, j] = 0;
                    }
                }
            }
        }
    }
}
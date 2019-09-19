﻿namespace unityremote
{
    public class LocalBrain : Brain
    {
        public Controller controller;
        
        public void Start()
        {
            agent.SetBrain(this);
            agent.StartData();
        }

        private void LocalDecision()
        {
            if (controller != null)
            {
                object[] msg = controller.GetAction();
                receivedcmd = (string)msg[0];
                receivedargs = (string[])msg[1];
                agent.ApplyAction();
                agent.UpdatePhysics();

                agent.UpdateState();
                agent.GetState();
            }
        }

        public void FixedUpdate()
        {
            if (fixedUpdate)
            {
                LocalDecision();
            }
        }

        public void Update()
        {
            if (!fixedUpdate)
            {
                LocalDecision();
            }
        }

        public override void SendMessage(string[] desc, byte[] tipo, string[] valor)
        {
            controller.ReceiveState(desc, tipo, valor);
        }
    }
}

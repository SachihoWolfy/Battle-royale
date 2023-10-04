using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerController : MonoBehaviourPun
{
    [Header("Stats")]
    public float moveSpeed;
    public float jumpForce;
    public float sprintSpeed;
    [Header("Components")]
    public Rigidbody rig;
    [Header("Arms and Headlight")]
    public GameObject arms;
    public GameObject head;
    [Header("Look Sensitivity")]
    public float sensX;
    public float sensY;
    [Header("Clamping")]
    public float minY;
    public float maxY;
    [Header("Spectator")]
    private float rotX;
    private float rotY;

    [Header("Photon")]

    public int id;
    public Player photonPlayer;

    [Header("Health 'N Things")]

    private int curAttackerId;
    public int curHp;
    public int maxHp;
    public int kills;
    public bool dead;
    private bool flashingDamage;
    public MeshRenderer mr;
    public PlayerWeapon weapon;
    [Header("Sounds")]
    public AudioSource AS;
    public AudioClip hurt;
    public AudioClip jump;
    public AudioClip heal;
    public AudioClip largeJump;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!photonView.IsMine || dead)
            return;
        Move();
        if (Input.GetKeyDown(KeyCode.Space))
            TryJump();
        if (Input.GetMouseButtonDown(0))
            weapon.TryShoot();
        if (Input.GetMouseButton(0))
        {
            weapon.TryRapidShoot();
            weapon.setIsFiring(true);
        }
        else
        {
            weapon.setIsFiring(false);
            weapon.stopFiring();
        }
        if (Input.GetMouseButtonDown(1))
            TryLargeJump();
    }
    void Move()
    {
        // get the input axis
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 dir;
        // calculate a direction relative to where we're facing
        if (Input.GetKey(KeyCode.LeftShift))
        {
            dir = (transform.forward * z + transform.right * x) * sprintSpeed;
        }
        else
        {
            dir = (transform.forward * z + transform.right * x) * moveSpeed;
        }
        dir.y = rig.velocity.y;
        // set that as our velocity
        rig.velocity = dir;
    }
    void TryJump()
    {
        // create a ray facing down
        Ray ray = new Ray(transform.position, Vector3.down);
        // shoot the raycast
        if (Physics.Raycast(ray, 1.5f))
        {
            rig.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            AS.PlayOneShot(jump);
        }
            
    }
    void TryLargeJump()
    {
        // create a ray facing down
        Ray ray = new Ray(transform.position, Vector3.down);
        // shoot the raycast
        if (Physics.Raycast(ray, 1.5f))
        {
            float ogMovespeed = moveSpeed;
            rig.AddForce(Vector3.up * jumpForce * 1.7f, ForceMode.Impulse);
            rig.AddForce(Vector3.forward * jumpForce, ForceMode.VelocityChange);
            AS.PlayOneShot(largeJump);
            while (!Physics.Raycast(ray, 1.5f)) { moveSpeed = ogMovespeed * 4f; }
            moveSpeed = ogMovespeed;
        }

    }

    private void LateUpdate()
    {
        rotateArms();
    }

    public void rotateArms()
    {
        if (photonView.IsMine)
        {
            // get the mouse movement inputs
            rotX += Input.GetAxis("Mouse X") * sensX;
            rotY += Input.GetAxis("Mouse Y") * sensY;

            // clamp the vertical rotation
            rotY = Mathf.Clamp(rotY, minY, maxY);
            // rotate the camera vertically
            arms.transform.localRotation = Quaternion.Euler(-rotY, 0, 0);
        }
    }

    [PunRPC]
    public void Initialize(Player player)
    {
        id = player.ActorNumber;
        photonPlayer = player;
        GameManager.instance.players[id - 1] = this;

        // is this not our local player?
        if (!photonView.IsMine)
        {
            GetComponentInChildren<Camera>().gameObject.SetActive(false);
            rig.isKinematic = true;
        }
        else
        {
            GameUI.instance.Initialize(this);
            head.SetActive(false);
        }
    }

    [PunRPC]
    public void TakeDamage(int attackerId, int damage)
    {
        if (dead)
        {
            return;
        }
        Debug.Log("Trying To Take Damage");
        AS.PlayOneShot(hurt);
        curHp -= damage;
        curAttackerId = attackerId;
        // flash the player red
        photonView.RPC("DamageFlash", RpcTarget.Others);
        // update the health bar UI
        GameUI.instance.UpdateHealthBar();
        // die if no health left
        if (curHp <= 0)
            photonView.RPC("Die", RpcTarget.All);
    }
    [PunRPC]
    void DamageFlash()
    {
        if (flashingDamage)
            return;
        StartCoroutine(DamageFlashCoRoutine());
        IEnumerator DamageFlashCoRoutine()
        {
            flashingDamage = true;
            Color defaultColor = mr.material.color;
            mr.material.color = Color.red;
            yield return new WaitForSeconds(0.05f);
            mr.material.color = defaultColor;
            flashingDamage = false;
        }
    }
    [PunRPC]
    void Die()
    {
        curHp = 0;
        dead = true;
        GameManager.instance.alivePlayers--;
        // host will check win condition
        if (PhotonNetwork.IsMasterClient)
            GameManager.instance.CheckWinCondition();
        // is this our local player?
        if (photonView.IsMine)
        {
            if (curAttackerId != 0)
                GameManager.instance.GetPlayer(curAttackerId).photonView.RPC("AddKill", RpcTarget.All);
            // set the cam to spectator
            GetComponentInChildren<CameraController>().SetAsSpectator();
            // disable the physics and hide the player
            rig.isKinematic = true;
            transform.position = new Vector3(0, -50, 0);
        }
    }
    [PunRPC]
    public void AddKill()
    {
        kills++;
        GameUI.instance.UpdatePlayerInfoText();
    }
    [PunRPC]
    public void Heal(int amountToHeal)
    {
        curHp = Mathf.Clamp(curHp + amountToHeal, 0, maxHp);
        AS.PlayOneShot(heal);
        // update the health bar UI
        GameUI.instance.UpdateHealthBar();
    }

}

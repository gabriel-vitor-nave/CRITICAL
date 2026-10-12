using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public int HP;
    public int maxHP;

    public TMP_Text hpText;

    private void Start()
    {
        hpText.text = "HP: " + HP + " / " + maxHP;
    }

    public void ChangeHP(int amount)
    {
        HP += amount;
        hpText.text = "HP: " + HP + " / " + maxHP;

        if(HP <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}

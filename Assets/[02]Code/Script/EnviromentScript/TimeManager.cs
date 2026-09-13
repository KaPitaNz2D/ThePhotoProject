using UnityEngine;

public class TimeManager : MonoBehaviour
{
    private int minutes;
    private int hours;
    private int days;
    
    private float tempSecond;

    private void Update(){
        tempSecond = Time.deltaTime;
        if(tempSecond > 1){
            minutes++;
            tempSecond = 0;
        }
    }
}

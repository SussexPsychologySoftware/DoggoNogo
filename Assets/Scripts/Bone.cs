using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.SceneManagement;
using UnityEngine.Networking; //Unity Web Request
using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic; //for List
using System.Runtime.InteropServices; //for DllImport
using TMPro; //for TextMeshProUGUI

public class Bone : MonoBehaviour
{

    // ******************* VARIABLES *******************

    // Inter-stimulus Intervals ------------------------------------------------------------
    // declare ISI array parameters/vars
    public double isi_low = 0.2; //note ISIs are doubles in line with Stopwatch.Elapsed.TotalSeconds - but consider ints e.g. 1400 ms to avoid point representation errors
    public double isi_high = 3.5;
    public double isi_step = 0.1;
    public int isi_rep = 3; //how many times to repeat each isi
    private double[] isi_array; // this stores all isis in single array - these are copied to data individually at start of each trial
    public int trial_limit = 2; //run only 3 trials - set to like -1 and shouldn't ever be actiavted.
    private double median_rt; //store median rt

    //timers
    public Stopwatch isi_timer = new Stopwatch(); // High precision timer: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch?view=net-8.0&redirectedfrom=MSDN#remarks
    public Stopwatch rt_timer = new Stopwatch(); // https://stackoverflow.com/questions/394020/how-accurate-is-system-diagnostics-stopwatch

    // Visuals ------------------------------------------------------------
    // setup stim display vars
    private float s; // or 0.4145592f original image is too big - can probably just prefab this in future
    Color forest = new Color(0.06770712f, 0.5817609f, 0f, 1f); //colour of positive feedback text

    // Scorecard
    public int score = 0; //holds score
    public TextMeshProUGUI scoreText; // displays score
    public TextMeshProUGUI feedbackText; //feedback
    public HealthBar healthBar;

    // Data ------------------------------------------------------------
    // trial-level data (globals)
    private int trial_number = -1; //tracks trial number
    private int early_presses = 0; // counts early button presses
    private double isi; //stores each trial's isi

    //consider multidimensional or jagged array? https://stackoverflow.com/questions/597720/differences-between-a-multidimensional-array-and-an-array-of-arrays
    ArrayList rts_array = new(); // Store rts in ArrayList to allow for easier median computation and store as sorted list (i.e. rts_array.Sort() method)
    
    // DATA CLASSES ------------------------------------------------------------
    //Create json-convertable struct to hold data, each trial stored individually https://forum.unity.com/threads/serialize-nested-objects-with-jsonutility.737624
    [System.Serializable]
    public class Metadata {
        public string id;
        public string name;
        public string userAgent;
        public string start;
        public string end;
    }

    [System.Serializable]
    public class Trial {
        public int trial_n;
        public double isi;
        public double rt;
        public string datetime;
        public int score;
        public int early_presses;
    }

    [System.Serializable]
    public class Data {
        public Metadata metadata;
        public List<Trial> trials;
        public Data() {
            metadata = new Metadata();
            trials = new List<Trial>();
        }
    }
    
    Data data = new Data(); //create instance


    // ******************* FUNCTIONS *******************
    // TIMING Helpers ------------------------------------------------------------
    //shuffle function for ISIs (Fisher-Yates shuffle should be fine)  https://stackoverflow.com/questions/1150646/card-shuffling-in-c-sharp
    void Shuffle(double[] array) {
        System.Random r = new System.Random();
        for (int n=array.Length-1; n>0; --n) {
            int k = r.Next(n+1); //next random on system iterator
            (array[k], array[n]) = (array[n], array[k]); //use tuple to swap elements
        }
    }

    public static double median(ArrayList array) { // slow but simple median function - quicker algorithms here: https://stackoverflow.com/questions/4140719/calculate-median-in-c-sharp
        //can maybe remove some of the doubles here?
        int size = array.Count;
        array.Sort(); //note mutates original list
        //get the median
        int mid = size / 2;
        double mid_value = (double)array[mid];
        double median = (size % 2 != 0) ? mid_value : (mid_value + (double)array[mid - 1]) / 2;
        return median;
    }

    public double roundTime(double time, int dp){
        return Math.Round(time *  Math.Pow(10, dp)) /  Math.Pow(10, dp); //remove trailing 0s - avoids double precision errors. or try .ToString("0.00") or .ToString("F2")
    }

    // METADATA --------------------------------
    // Grab userAgent - Not working
    public class UA : MonoBehaviour { //https://stackoverflow.com/questions/72083612/detect-mobile-client-in-webgl
        [DllImport("__Internal")] // imports userAgent() from Assets/WebGL/Plugins/userAgent.jslib
        static extern string userAgent();
        public static string getUserAgent(){
            #if UNITY_EDITOR
                    return "EDITOR"; // value to return in Play Mode (in the editor)
            #elif UNITY_WEBGL
                    return userAgent(); // value based on the current browser
            #else
                    return "NO_UA";
            #endif
        }
    }

    //random ID generator
    System.Random rand = new System.Random(); 
    public const string characters = "abcdefghijklmnopqrstuvwyxzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public string GenerateString(int size) { //https://stackoverflow.com/a/9995960/7705626
        char[] chars = new char[size];
        for (int i=0; i < size; i++) {
            chars[i] = characters[rand.Next(characters.Length)];
        }
        return new string(chars);
    }

    void initMetadata(){
        // Create metadata (init saves start time) - void as attached to global var
        data.metadata.id = GenerateString(24); // Assign id
        data.metadata.name = PlayerPrefs.GetString("Name", "No Name");
        data.metadata.userAgent = UA.getUserAgent(); // Assign userAgent
        data.metadata.start = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // Assign date
    }

    void saveMetadata(){ //stores end time of exp
        data.metadata.end = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); // Assign date
    }


    // TRIAL MANAGEMENT ------------------------------------------------------------
    void saveTrialData(double rt, string datetime){ //save current variables to an instance of the trial class
        Trial trial_data = new Trial(); // Create an instance of a Trial
        trial_data.trial_n = trial_number+1;
        trial_data.isi = isi; 
        trial_data.rt = roundTime(rt,7); // round off to avoid precision errors - 7 is length of ElapsedTicks anyway.
        trial_data.datetime = datetime
        trial_data.score = score;
        trial_data.early_presses = early_presses;
        data.trials.Add(trial_data); // Add trial object to the list of trials
    }

    void newTrial() { //function to reset variables and set-up for a new trials
        //reset vars
        trial_number++;
        early_presses = 0;
        gameObject.transform.localScale = Vector3.zero; // reset stim
        isi = isi_array[trial_number]; // new isi
        // reset timers
        isi_timer.Reset();
        isi_timer.Start();
        rt_timer.Reset();
    }

    void endExp(){
        //Send data
        PlayerPrefs.SetInt("Score", score); //save score to local copy
        PlayerPrefs.Save();
        saveMetadata();
        string json = jsonify(data);
        StartCoroutine(sendData(json));
        // Next scene
        SceneManager.LoadScene("End");
    }

    // SENDING DATA -------------------------------------
    [System.Serializable] //class to format the data as expected by datapipe
    public class DataPipeBody{
        public string experimentID;
        public string filename;
        public string data; //json string of data object
    }

    string jsonify(Data data){ //Create data in string as expected by datapipe
        //Data data passes in function scoped copy - probably unnecessary...
        DataPipeBody body = new DataPipeBody(); //create instance
        body.experimentID = "VSyXogVR8oTS";
        body.filename = data.metadata.name + "_" + data.metadata.id + ".json";
        body.data = JsonUtility.ToJson(data); 
        string json = JsonUtility.ToJson(body); //Note double encoding is necessary here as looks like datapipe parses this as an object on their end too
        Debug.Log(json);
        return json;
    }

    IEnumerator sendData(string json){ //sends data - IEnumerator can run over sever frames and wait for 'OK' response from OSF server
        using (UnityWebRequest www = UnityWebRequest.Post("https://pipe.jspsych.org/api/data/", json, "application/json")) {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success) {
                Debug.LogError(www.error);
            }
            else {
                Debug.Log("Form upload complete!");
            }
        }
    }



    // Start --------------------------------
    void Start()
    {
        s = gameObject.transform.localScale.x;
        initMetadata(); //adds most of the global metadata vars
        // Create isi array
        int isi_array_length = (int)Math.Ceiling((isi_high-isi_low)/isi_step +1); //round up for floating point errors
        isi_array = new double[isi_array_length*isi_rep]; //length of each set * number of repeats
        for (int j=0; j<isi_rep; j++) { //loop repeats of each number
            int set = isi_array_length*j; //add length of one set of numbers to current index
            for (int i=0; i<isi_array_length; i++) { //loop through each increment to isi
                isi_array[set+i] = roundTime(isi_low + i * isi_step,1);
            }
        } // LOG: foreach (float value in isi_array){Debug.Log(value);}  
        Shuffle(isi_array); //shuffle array

        //setup first trial
        newTrial();
    }



    // ******************* TRIAL RUNNER *******************

    // Update is called once per frame - maybe use FixedUpdate for inputs?
    void Update()
    {
       if(isi_timer.IsRunning){ //if in isi/ not waiting for reaction
            //handle early presses
            if(Input.GetKeyDown("space")){
                 score -= 2; // minus 2 points for an early press
                if(score<0){ 
                    score = 0; //lowerbound on score of 0
                }
                scoreText.text = "Score: " + score;
                feedbackText.color = Color.red;
                feedbackText.text = "TOO QUICK!\nWait until the bone has appeared.";
                early_presses++;
            }

            //when timer runs out
            if(isi_timer.Elapsed.TotalSeconds >= isi){ // timer.ElapsedMilliseconds less precise int, Elapsed.TotalSeconds = double, timer.ElapsedTicks most precise
                feedbackText.text = ""; //hide feedback
                gameObject.transform.localScale = new Vector3(s,s,s); //show bone
                //timers
                isi_timer.Stop();
                rt_timer.Start();
            }

        } else { //when waiting for input
            if((rts_array.Count>0 && rt_timer.Elapsed.TotalSeconds>(median_rt+.1)) || rt_timer.Elapsed.TotalSeconds>1.5){ //if time is greater than (median + 100 msec) or 1.5sec hide the bone
                gameObject.transform.localScale = Vector3.zero; //hide bone
            }

            //on reaction
            if(Input.GetKeyDown("space")){ 
                
                //get data
                rt_timer.Stop();
                string datetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                double rt = rt_timer.Elapsed.TotalSeconds; //consider changing data types ElapsedMilliseconds
                //rts[trial_number] = rt; //being lazy and using two copies of rt arrays here
                rts_array.Add(rt); //ArrayList version for easier median, could deep-copy in function.
                // median
                median_rt = median(rts_array);
                
                // CALCULATE SCORE ******************************

                //float m = (float)(median_rt-rt==0 ? rt : median_rt-rt); // if no difference then return rt
                //float log_m = m<0 ? Mathf.Log(1+Mathf.Abs(m))*-1 : Mathf.Log(1+m); //cannot take negative log
                //double before_rounding = 1/rt * log_m;
                //int logscore = (int)Math.Round(before_rounding); //final score for this method
                //int mscore = (int)Math.Round(1/rt + (median_rt-rt)*1.5); //simple method

                //******************************

                if(rt<(median_rt+.1)){ //if within 100ms of median
                    score += 3;
                    feedbackText.color = forest;
                    feedbackText.text = "YUMMY!\nDoggo caught the bone!";       
                } else {
                    score += 1;
                    feedbackText.color = Color.blue;
                    feedbackText.text = "Good!\nDoggo fetched the bone.";
                }
                scoreText.text = "Score: " + score;
                healthBar.SetHealth(score);
                //******************************

                //store data
                //data.score[trial_number] = score;
                // END OF TRIAL
                saveTrialData(rt,datetime);
                if(trial_number == isi_array.Length-1 || trial_number == trial_limit ){
                    endExp();
                } else {
                    newTrial();
                } 
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace Queens;

class Program
{   // List of all Manual reset events being created
    public static List<ManualResetEvent> mres;
    // Delegate to be used to pass the backtracking method to threads
    public delegate void Del((List<int>, Dictionary<int, HashSet<int>>)? objects);
    
    public static Del method;
    //Completed puzzle
    public static List<int>? poss;

/// <summary>
/// Counts the number of conflicts in a state
/// </summary>
/// <param name="state">The state to be evaluated</param>
/// <returns>The number of conflicts in a state</returns>
    private static int h(List<int> state)
    {
        int n = state.Count;

        int tot = 0;
        for (int r1 = 0; r1 < n; r1++)
        {
            int c1 = state[r1];
            for (int r2 = r1 + 1; r2 < n; r2++)
            {
                int c2 = state[r2];
                if (c1 == c2 || Math.Abs(r1 - r2) == Math.Abs(c1 - c2))
                {
                    tot++;
                }
            }

        }

        return tot;
    }
    
/// <summary>
/// Gets the option with the least amount of states that you can create
/// </summary>
/// <param name="options">A dictionary with the options of a state</param>
/// <returns>The key of thhe dict that has the fewest options</returns>
    private static int Min(Dictionary<int, HashSet<int>> options)
    {
        int? min =null;
        int? key = null;
        foreach (KeyValuePair<int, HashSet<int>> i in options)
        {
            if (min == null)
            {
                min =  options[i.Key].Count;
                key = i.Key;
            }
            else if(options[i.Key].Count < min)
            {
                min =  options[i.Key].Count;
                key = i.Key;

            }
        }

       
        if (key == null )
        {
            throw new Exception("Key null exception, Check options");
        }

        return (int) key;
    }
    /// <summary>
    /// Creates an empty conflicts list
    /// </summary>
    /// <param name="options">Gives a list of possible conflicts as keys given the options in an environment</param>
    /// <returns>A dict with keys as each row and a empty set</returns>
    private static Dictionary<int, HashSet<int>> conflict_list(Dictionary<int, HashSet<int>> options)
    {   // Dictionary to be returned 
        Dictionary<int, HashSet<int>> ret = new Dictionary<int, HashSet<int>>();
        foreach (KeyValuePair<int, HashSet<int>> i in options)
        {
            ret[i.Key] = new HashSet<int>();
        }

        return ret;
    }
    //Global variable, helps with regulating multithreading 
    private static Lock lok = new Lock();

    /// <summary>
    /// Recursive method that solves the queen puzzle of any size, utilizing backtracking 
    /// </summary>
    /// <param name="state">Queen Puzzle state</param>
    /// <param name="options">All the options that this queen puzzle state can become </param>
    /// <returns>Not supposed to return anything</returns>
    public static List<int>? Complete(List<int> state, Dictionary<int, HashSet<int>> options)
    {
        // If we have no more options then we have a completed state
        if (options.Count == 0)
        {

            // Assigns the state to the global variable 
            lock (lok)
            {
                poss = state;
                return state;
            }

        }

        //Gets a row with the fewest remaining options 
        int row = Min(options);
        // Gets the set that has all the options in a row
        HashSet<int> pCol = options[row];
        //Removes the row from all the options
        options.Remove(row);
        //Iterate through the all possible columns 
        foreach (int c1 in pCol)
        {
            //Tries to put the queen for this row in c1 column
            state[row] = c1;

            //Dictionary with all options left as keys and empty sets to use for conflicts 
            Dictionary<int, HashSet<int>> conflicts = conflict_list(options);
            //Iterates through all options to check for conflicts
            foreach (KeyValuePair<int, HashSet<int>> i in options)
            {
                // the row we are checking for a conflict
                int r2 = i.Key;
                // Every conflict it could be 
                int[] con_list = [c1, r2 - row + c1, row - r2 + c1];

                foreach (int x in con_list)
                {
                    //if a row has a col in the conflict list then ass them to the conflicts
                    if (options[r2].Contains(x))
                    {
                        conflicts[r2].Add(x);
                    }
                }

            }
            //loops through options and removes each conflict 
            foreach (KeyValuePair<int, HashSet<int>> i in options)
            {
                int r2 = i.Key;
                foreach (int w in conflicts[r2])
                {
                    //Remove the col from the possible options of a row
                    options[r2].Remove(w);
                }
            }

            // Recursively call complete 
            List<int>? sol = Complete(state, options);
            if (sol != null)
            {
                return sol;
            }
            //Backtracking
            state[row] = -1;
            foreach (KeyValuePair<int, HashSet<int>> i in options)
            {
                int r2 = i.Key;
                foreach (int w in conflicts[r2])
                {

                    options[r2].Add(w);
                }
            }


        }

        options[row] = pCol;
        return null;

    }
    
    public static (List<int>, Dictionary<int, HashSet<int>>) Create(int n)
    {
        List<int> ret1 = new List<int>();
        Dictionary<int, HashSet<int>> r2 = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < n; i++)
        {
            ret1.Add(-1);
        }

        for (int i = 0; i < n; i++)
        {
            HashSet<int> hash = new HashSet<int>();
            for (int x = 0; x < n; x++)
            {
                hash.Add(x);
            }

            r2.Add(i, hash);
        }

        return (ret1, r2);

    }
    /// <summary>
    /// Void Method of complete to be able to pass to the threadpool
    /// </summary>
    /// <param name="objects"></param>
    private static void  Complete2((List<int>, Dictionary<int, HashSet<int>>)? objects)
    {
        var (state, options) = (ValueTuple<List<int>, Dictionary<int, HashSet<int>>>)objects!;

        Complete(state, options);
        return;
    }

    private static List<int> copy_state(List<int> x)
    {
        List<int> ret = new List<int>();
        foreach (var va in x)
        {
            ret.Add(va);
        }

        return ret;
    }
    /// <summary>
    /// Parses the state and queues the work in the Threadpool 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="options"></param>
    /// <param name="method"></param>
    public static void Start(List<int> state, Dictionary<int, HashSet<int>> options, Del method)
    {
        //How many queens there are
        int n = state.Count;
        //Given each first queen start state a thread
        for (int i = 0; i < n; i++)
        {
            // Empty state except the first index is each place the first queen could be
            List<int> newState = new List<int>(state) { [0] = i }; 
            Dictionary<int, HashSet<int>> newOptions = options.ToDictionary(entry => entry.Key, entry => new HashSet<int>(entry.Value));
            HashSet<int> temp = new HashSet<int>();
            foreach (int x in newOptions[i])
            {
                temp.Add(x);
            }

            newOptions.Remove(i);
           //mre for each thread queue 
            ManualResetEvent mre = new ManualResetEvent(false);
            mres.Add(mre);
            var input =
              (newState, newOptions, mre);
            ThreadPool.QueueUserWorkItem(obj =>
            {
                var (state, options, mre) =
                    (ValueTuple<List<int>, Dictionary<int, HashSet<int>>, ManualResetEvent>)obj!;
                method((state, options));
                mre.Set();


            }, input);
            newOptions.Add(i, temp);


    


            
        }
        

        return;


    }
    
    static void Main(string[] args)
    {

        mres = new List<ManualResetEvent>();

        method = Complete2;
        
        var (state, opt) = Create(400);
        Start(state, opt, method);
        // Need to batch the queue
        while (poss == null)
        {
            int sync = WaitHandle.WaitAny(mres.ToArray()); 
            mres.Remove(mres[sync]);
            //
            
        }

        Console.WriteLine(h(poss));







    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

//create n-Threads -> each thread search the string in it's own text section, by reading it into buffers.
//if the desired string was found- return the correct index and finish the program (notify all threads to finish)
//the program deals with overlapping sections between the threads (handle cases of strings which located in the middle of two-threads sections)

class Program
{

    //Input args can be assumed to be correct
    public static void Main(string[] args)
    {
        string textfile = args[0]; // the text file to search in
        string StringToSearch = args[1];  //the string to search in the text file (it contains only letters & digits and no spaces)
        int nThreads = Convert.ToInt32(args[2]); //How many threads will be created for the search
        int Delta = Convert.ToInt32(args[3]); //The delta (distance) between letters to search in the file

        int Middle = Math.Max((StringToSearch.Length * (Delta+1)), StringToSearch.Length); //Middle = the maximum range of letters for the requested string
        CancellationTokenSource _tokenSource = new CancellationTokenSource(); //create CancellationTokenSource - pointer to tokenSource
        CancellationToken _token = _tokenSource.Token; //create CancellationToken

        //create n threads, each one will get it's own parameters for the search
        try
        {
            //for our use test - Input args can be assumed to be correct (include nThreads >= 1)
            if (nThreads < 1)
            {
                Console.WriteLine("nThreads must be at least 1");
                return;
            }

            using (var sr = new StreamReader(textfile))  // Open the text file using a stream reader
            {
                //find indexes of text-sections for each thread (each tread will take (FileLength / nThreads) chars, +- Middle for each side
                long FileLength = sr.BaseStream.Length; //find the length of the text
                long LengthFoEachThreasPart = (FileLength / nThreads); //find the length of text-section of each thread

                //create two dimentional array of start & end indexes for each thread
                long[,] indexes = new long[nThreads, 2];

                //update indexes of start & end for each thread (LengthFoEachThreasPart size)
                long currS = 0;
                for (int i = 0; i < nThreads; i++)
                {
                    indexes[i, 0] = currS; //update start thread index
                    indexes[i, 1] = (currS+ LengthFoEachThreasPart-1); //update end thread index
                    currS = (currS + LengthFoEachThreasPart);
                }

                //add middle-value before & middle-value after to indexes
                for (int i =1; i< nThreads; i++) //update the rest of the indexes
                {
                    indexes[i, 0] = Math.Max(indexes[i, 0] - Middle,0); //update start thread index
                    //indexes[i, 0] = indexes[i, 0] - Middle; //update start thread index
                    long stop = Math.Min(indexes[i, 1] + Middle, FileLength-1); //make sure we won't step over the end index of the text
                    indexes[i, 1] = stop; //update end thread index
                }

                indexes[0, 1] = Math.Min(indexes[0, 1] + Middle, FileLength - 1);   //update first thread index with + Middle (or FileLength if we have one thread)
                indexes[nThreads - 1, 1] = FileLength-1;

                //create an array of nThreads (given value), each thread will apply threadWork function
                Thread[] threads = new Thread[nThreads]; 
                for (int i = 0; i < nThreads; i++)
                {
                    threads[i] = new Thread(threadWork);
                }

                //start each thread with the needed argument
                for (int i = 0; i < nThreads; i++)
                {
                    //each thread will get the following arguments:
                    //textFile-name (Address) , Delta, StringToSearch, indexes of it's search-range: start & end, and _tokenSource

                    object[] argumentsToThread = new object[6];

                    argumentsToThread[0] = textfile; //textFile name (like address)
                    argumentsToThread[1] = Delta; //Delta
                    argumentsToThread[2] = StringToSearch; //StringToSearch
                    argumentsToThread[3] = indexes[i, 0]; //start index
                    argumentsToThread[4] = indexes[i, 1]; //end index
                    argumentsToThread[5] = _tokenSource; //CancellationToken

                    threads[i].Start(argumentsToThread); //start the thread, with it's array of arguments
                }

                //Main thread will wait for all threads to finish their search
                for (int i = 0; i < nThreads; i++)
                {
                    threads[i].Join();
                }

                if (_token.IsCancellationRequested) //IsCancellationRequested=true only if one of the threads found a solution
                {
                    return;
                }

                //else - print "output not found" and finish the program
                Console.WriteLine("output not found");
                return;
            }
        }
        catch (IOException e)
        {
            //for our use test - Input args can be assumed to be correct
            Console.WriteLine("The file could not be read");
            Console.WriteLine(e.Message);
        }}

    //thread function - divide thead text part, and sent to search function 10k of chars each time
    public static void threadWork(Object args) {

        //arguments contains: textFile-name, Delta, StringToSearch, indexes of it's search-range: start & end, and _tokenSource
        Object[] arguments = (Object[])args;

        //convert arguments to their 'real' type
        string text = Convert.ToString(arguments[0]);
        int delta = Convert.ToInt32(arguments[1]);
        string StrToSearch = Convert.ToString(arguments[2]);
        long start = Convert.ToInt64(arguments[3]);
        long end = Convert.ToInt64(arguments[4]);
        CancellationTokenSource token_Source = (CancellationTokenSource)arguments[5];
        CancellationToken _token = token_Source.Token; //save the CancellationToken


        //Variables for this split part of the function
        int Middle = Math.Max((StrToSearch.Length * (delta+1)), StrToSearch.Length); //Middle = the maximum range of letters for the requested string
        const int bufferSize = 10000; //save each part in a buffer with the size of 10k
        if (Middle >= bufferSize) { //if the length of the string and the delte equals to 10k, than the overlap between any two buffers will be 9,999 (1 char forward in each search)
            Middle = (bufferSize - 1);
        }
        var firtsTime = true; //mark first divided part
        var counter = bufferSize; //will save how many chars we have read in each iteration

        long currStartIndexInBuffer = 0; //how many chars we have read until this section (the index to count from)
        long CharsToRead = (end - start + 1); //how many chars we need to read at total in this thread

        try
        {
            using (var sr = new StreamReader(text))  // Open the text file using a stream reader
            {
                //remove all previous letters (belongs to the previous threads)
                for (long i = 0; i < start; i++) {
                    sr.Read();
                }

                //split the content - each time- read 10k chars into buffer, and search the string in each section
                //in each buffer, we will take the last middle-chars from the prev buffer (handle splits)

                String temp = null; //will use to add Middle chars from buffer to buffer (handle the splits)
                while ((counter > 0) && ((CharsToRead - currStartIndexInBuffer) > 0)) //while we still have chars to read
                {
                    if (_token.IsCancellationRequested) //if token IsCancellationRequested=true - stop running and return
                    {
                        return;
                    }

                        if (firtsTime) //first iteration
                    {
                        firtsTime = false;
                        var sb = new StringBuilder(); //create empty string (will save the text)
                        var buffer = new Char[bufferSize];  //create new buffer of chars (size = 10k)
                        counter = sr.Read(buffer, 0, bufferSize); //read sr into buffer, between indexes: 0-bufferSize. conter = how many chars we have read in each iteration
                        int stop = (int)Math.Min(counter, CharsToRead- currStartIndexInBuffer); //stop = last index of the part that we want to save from the buffer
                        sb.Append(buffer, 0, stop); //append buffer content into sb (until stop index)
                        int start_I = Math.Max(counter - Middle, 0); //start_I = last Middle-char in the buffer (add them to the next group of chars)
                        for (int i = 0; i < Middle; i++) //add all those chars to temp
                        {
                            temp += buffer[start_I];
                            start_I++;
                        }
                        //check if the requasted string is in this part of the file, if it is - return it's index, else- return -1
                        long Search = SaerchInText(sb, delta, StrToSearch);
                        if (Search >= 0) //success
                        {
                            if (_token.IsCancellationRequested)  //if token IsCancellationRequested=true - stop running and return
                            {
                                return;
                            }
                            //return the corrent index of the string - using the index of the thread-text-part + index of the buffer + Search-result
                            Console.WriteLine("output: " + (Search + start + currStartIndexInBuffer)); //print the result! 
                            token_Source.Cancel(); //Cancel all threads
                        }
                        currStartIndexInBuffer = currStartIndexInBuffer + counter - Middle; //update currStartIndexInBuffer
                    }
                    else
                    {
                        //same comments as above - only buffer update is different
                        var sb = new StringBuilder();
                        sb.Append(temp);
                        temp = null;
                        var buffer = new Char[bufferSize];
                        counter = sr.Read(buffer, Middle, bufferSize - Middle);
                        int stop = (int)Math.Min(counter, CharsToRead - currStartIndexInBuffer);
                        sb.Append(buffer, Middle, stop);
                        int start_I = Math.Max(counter, 0); 
                        for (int i = 0; i < Middle; i++)
                        {
                            temp += buffer[start_I];
                            start_I++;
                        }
                        long Search = SaerchInText(sb, delta, StrToSearch); 
                        if (Search >= 0) 
                        {
                            if (_token.IsCancellationRequested)
                            {
                                return;
                            }
                            Console.WriteLine("output: " + (Search+ start + currStartIndexInBuffer)); 
                            token_Source.Cancel();
                        }
                        currStartIndexInBuffer = currStartIndexInBuffer + counter;
                    }
                }
            }
        }
        catch (IOException e)
        {
            Console.WriteLine("The file could not be read");
            Console.WriteLine(e.Message);
        }

    }

    //textForSearch = the file part to search in (10k chars size maximum)
    //Delta= The delta (distance) between letters to search in the file
    //StringToSearch = the string to search in the text file (it contains only letters & digits and no spaces)
    public static long SaerchInText(StringBuilder textForSearch, int Delta, string StringToSearch)
    {

        // this function finds the given string in the given text file and return it's index (if exist), else - return -1

        int count = 0; //will count how many optional indexes we have to check (by finding the indexes of first char in the text)
        var IndexList = new List<int>();  //create an empty list of ints (will hold possible indexes of the wanted value)
        char FirstChar = StringToSearch[0]; //first letter of the given string

        //find all indexes of chars in the text that are equal to the first lette in StringToSearch
        for (int index = 0; index < textForSearch.Length; index++)
        {
            if (textForSearch[index] == FirstChar)
            {
                IndexList.Add(index);
                count++;
            }
        }

        if (count == 0) { 
            return -1; }  //this first letter isn't it the text at all

        //iterate over all this indexes, and look for the whole string (check if it starts from this index)
        //if we have found the strign - return with it's index
        for (int i = 0; i < count; i++)
        {
            String temp = null;
            temp += FirstChar; //will hold the temporary checked string
            int countDelta = 1; //how many times we need to tadd delta to the start index of the string
            int CurrStartIntdex = IndexList[i]; //the checked index of the first letter in StringToSearch
            for (int j = 0; j < (StringToSearch.Length - 1); j++) //add (StringToSearch.Length - 1) with delta spaces to temp, and check if it's equal to StringToSearch 
            {
                if ((CurrStartIntdex + (countDelta * (Delta + 1))) >= textForSearch.Length) //index out of bound
                {
                    return -1;
                }
                temp += textForSearch[CurrStartIntdex + (countDelta * (Delta + 1))];
                countDelta++;
            }
            if (temp.Equals(StringToSearch)){ //if we have find the StringToSearch - return it's index in this given text
                return (CurrStartIntdex);
            }
        }
        //if we have reached here - output was not found
        return -1;
    }


}


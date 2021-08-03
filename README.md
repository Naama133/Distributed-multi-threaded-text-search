# Distributed multi-threaded text search in large files (C#):

The program takes four parameters:
1. Textfile: the text file to search in
2. StringToSearch: the string to search in the text file (contains letters & digits, no spaces)
3. nThreads: How many threads will be created for the search
4. Delta: The distance between letters-to-search in the file (For example, the string "HELLO WORLD" contains the string "HLO" with delta=1)

The program output is the location (index) of the StringToSearch in the file (if exists).
If StringToSearch exists more than once in the text, the program output will be the location of the first instance detected by one of the threads.


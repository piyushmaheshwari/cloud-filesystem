using System;
using System.Collections.Generic;
using System.IO;


//Author - Piyush
namespace persistentbackend
{
	public class FileUtils
	{
		private static readonly log4net.ILog logger = 
			log4net.LogManager.GetLogger(typeof(FileUtils));

		public static List<string> ReadLinesFromFile(string filename){

			logger.Debug ("Reading lines from file" + filename);
			System.IO.StreamReader file = 
				new System.IO.StreamReader(filename);
			List<string>retlist = new List<string>();
			string line;

			while((line = file.ReadLine()) != null){
				retlist.Add(line);
			}
			file.Close();
			return retlist;
		}
		
		
		public static string getMemoryPathFromDiskPath (string diskpath)
		{
			return diskpath.Replace ("/", "_");
		}
		
		public static string getDiskPathFromMemoryPath (string memorypath)
		{
			return memorypath.Replace ("_", "/");
		}
		
	}
}


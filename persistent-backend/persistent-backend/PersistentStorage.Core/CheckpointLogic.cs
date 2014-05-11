using System;
using System.IO;
using System.Collections.Generic;

/*Author - piyush*/
namespace persistentbackend
{
	/*Core check pointing logic lives here*/
	public class CheckpointLogic
	{
		private static readonly log4net.ILog logger = 
			log4net.LogManager.GetLogger(typeof(CheckpointLogic));

		//This works on linux. Will read this from config later and make this OS agnostic
		private string pathprefix = "/home/piyush/persistent-disk/"; 


		private string USERINFOFILENAME = "user-info.txt";
		
		private string FILEINFOFILENAME = "file-info.txt";
		
		private string SHAREDFILEINFONAME = "shared-info.txt";

		/*Name of file which stores the last check point path*/
		private string lastcheckpointfilename = "lastcheckpoint.txt";

		public CheckpointLogic (){} //default contructor

		public CheckPointObject RestoreFileSystem (bool restoreFileContent)
		{
			logger.Debug ("Restoring file system");
			CheckPointObject checkObject = new CheckPointObject ();
			string lastcheckpointfilepath = pathprefix + lastcheckpointfilename;

			List<string> lastcheckpointfilecontent = FileUtils.ReadLinesFromFile (lastcheckpointfilepath);
			logger.Debug ("Last check point time stamp read :" + lastcheckpointfilecontent [0].Trim ());
			DateTime lastcheckpointtime = DateUtils.getDatefromString (lastcheckpointfilecontent [0].Trim ());

			checkObject.lastcheckpoint = lastcheckpointtime;
			logger.Debug ("Poop : " + checkObject.lastcheckpoint);
			if (lastcheckpointfilecontent.Count < 2)
				throw new DiskuserMetaDataCorrupt ("Something wrong with the last check point file path, check!!");
			
			string latestcheckpointfolderpath = lastcheckpointfilecontent [1]; 
			logger.Debug("Last check point path read as :" + latestcheckpointfolderpath);
			latestcheckpointfolderpath = latestcheckpointfolderpath.Trim (); //remove any leading or trailing whitespaces
			foreach (string userfolder in Directory.EnumerateDirectories(latestcheckpointfolderpath)) {
				checkObject.userfilesystemlist.Add(RestoreUserFileSystem(userfolder, restoreFileContent));	
			}

			return checkObject;
		}
		
		private UserFileSystem RestoreUserFileSystem (string userdir, bool restoreFileContent)
		{
			logger.Debug ("Restoring user file system for user dir path :" + userdir);
			UserFileSystem userfilesystem = new UserFileSystem ();
			string user = new DirectoryInfo (userdir).Name;
			
			string userinfofilepath = userdir + Path.DirectorySeparatorChar + this.USERINFOFILENAME;
			string fileinfofileapth = userdir + Path.DirectorySeparatorChar + this.FILEINFOFILENAME;
			string sharedfileinfofilepath = userdir + Path.DirectorySeparatorChar + this.SHAREDFILEINFONAME;
			
			//First restore the user info
			List<String> metadatafilecontent = FileUtils.ReadLinesFromFile ();
			if (metadatafilecontent.Count < 3) {
				throw new DiskuserMetaDataCorrupt ("Disk meta data corrupt for user: " + user);
			}

			//add the user meta data into the file system
			userfilesystem.metadata = new UserMetaData (
				metadatafilecontent [0].Trim (), 
				metadatafilecontent [1].Trim (),
				int.Parse (metadatafilecontent [2].Trim ())
			);
			
			//now restore the shared files
			List<string> sharedFileNames = FileUtils.ReadLinesFromFile (sharedfileinfofilepath);
			logger.Debug ("# shared file : " + sharedFileNames.Count);
			foreach (string sharefilepath in sharedFileNames) {
				userfilesystem.sharedFiles.Add (sharefilepath.Trim ());
			}
			
			//now restore the normal files
			List<string> userfilenames = FileUtils.ReadLinesFromFile (fileinfofileapth);
			logger.Debug ("# Owned file : " + userfilenames.Count);
			
			foreach (string filepath in userfilenames) {
				UserFile file = RestoreUserFile(userdir, filepath.Trim(), restoreFileContent);
				userfilesystem.filemap.Add(filepath.Trim(), file);
			}
			return userfilesystem;
		}


		private UserFile RestoreUserFile (string userdir, string relativefilepath, bool restoreFileContent)
		{	
			logger.Debug ("Restoring user file for file path and flag :" + userdir + relativefilepath + " " + restoreFileContent);
			string completefilepath = userdir + Path.DirectorySeparatorChar 
				+ "files" + Path.DirectorySeparatorChar + relativefilepath;
			string metadatafilepath = userdir + Path.DirectorySeparatorChar 
				+ "metadata" + Path.DirectorySeparatorChar + relativefilepath + ".dat";

			List<string> userfilemetadata = FileUtils.ReadLinesFromFile (metadatafilepath);
			
			if (userfilemetadata.Count < 2)
				throw DiskuserMetaDataCorrupt ("File meta data corrupt for file  : " + relativefilepath);
			
			UserFile file = GetFileFromFileMetaData (relativefilepath, userfilemetadata);
			
			//this makes sure that in case of checkpointing this stuff, we don't load the file content, I know it's pretty neat :)
			if (restoreFileContent) 
				file.filecontent = File.ReadAllBytes(completefilepath);
			
			return file;
		}

		private UserFile GetFileFromFileMetaData (string filepath, List<string> metadata)
		{
			UserFile file = new UserFile (filepath, metadata [0].Trim ());
			file.filepath = filepath;
			
			file.filesize = int.Parse (metadata [1].Trim ());
			file.versionNumber = int.Parse (metadata [2].Trim ());

			if (metadata.Count > 3) { //this is because this file might be shared with no one
				string [] clients = metadata [3].Trim ().Split (',');
			
				foreach (string client in clients) {
					if (client.Trim ().Length > 0) {
						file.sharedwithclients.Add (client.Trim ());
					}
				}
			}
			return file;
		}

		/*Check point entry */
		public void DoCheckPoint (CheckPointObject filesystem)
		{
			logger.Debug ("Do Check point method called for filesystem");
			
			CheckPointObject oldcheckpointobject = RestoreFileSystem (false);
			
			filesystem = mergeCheckPointObjects (filesystem, oldcheckpointobject);
			
			try{
				string path = GenerateCheckpointPath (filesystem.lastcheckpoint);
				logger.Debug ("Creating checkpointing path :" + path);
				Directory.CreateDirectory (path);
				string lastcheckpointfilepath = this.pathprefix + this.lastcheckpointfilename;
				foreach( UserFileSystem userfs in filesystem.userfilesystemlist){
					DoCheckPointForUser( userfs.metadata.clientId, path, userfs);
				}
			
				//Now update the last check point file so that we can restore this checkpoint
				logger.Debug ("Writing to last checkpoint file at path : " + lastcheckpointfilepath);
				System.IO.File.WriteAllText(lastcheckpointfilepath, 
				          DateUtils.getStringfromDateTime(filesystem.lastcheckpoint) + "\n" + path);

			}catch ( Exception e){
					logger.Debug ("Exception :" + e);
				throw e;
			}

		}

		private CheckPointObject mergeCheckPointObjects (CheckPointObject newimage, CheckPointObject oldimage)
		{
			
			logger.Debug("Merge check point objects");
			CheckPointObject retObject = new CheckPointObject (); //ret object
			foreach (UserFileSystem oldfs in oldimage) {
				foreach (UserFileSystem newfs in newimage) {
					if (oldfs.metadata.clientId.Equals (newfs.metadata.clientId)) {
						retObject.userfilesystemlist = mergeUserFileSystems (newfs, oldfs);				
					}
				}
			}
			return retObject;
			
		}
		
		private UserFileSystem mergeUserFileSystems (UserFileSystem newfs, UserFileSystem oldfs)
		{
			logger.Debug ("Merging user file systems for user id : " + newfs.metadata.clientId);
			
			
		}
		
		
		private CheckPointObject loadPreviousCheckObjectFromDisk ()
		{
			logger.Debug ("Loading previous check point contents from disk");
			
			/*string lastcheckpointfilepath = this.pathprefix + this.lastcheckpointfilename;
			List<string> lastCheckPointFileContent = FileUtils.ReadLinesFromFile (lastcheckpointfilepath);
			if (lastCheckPointFileContent.Count < 2)
				throw Exception ("Something wrong with the last check point file path, check!!");
			string goInFolder = lastCheckPointFileContent [1];
			logger.Debug ("Last check point folder path is : " + goInFolder);
			*/
			
			
			
		}
		
		public void DoCheckPointForUser (
			string user,
			string path,
			UserFileSystem userfilesystem){

			logger.Debug ("Check pointing for user :" + user);

			string userpath = path + user + Path.DirectorySeparatorChar; //Appending '/' for linux and '\' on windows
			Directory.CreateDirectory (userpath);
			string metadatafilepath = userpath + this.metadatafilename;

			string metadata = userfilesystem.metadata.clientId + 
							"\n" + userfilesystem.metadata.password
							+ "\n" + userfilesystem.metadata.versionNumber + "\n";

			List<String> files = userfilesystem.GetFileList ();
			foreach (string filename in files) {
				metadata += filename + "\n";
			}

			//Update the meta data file
			logger.Debug ("Writing meta file at path :" + metadatafilepath + " with content :\n" + metadata);
			System.IO.File.WriteAllText (metadatafilepath, metadata);

			//Now we write the file content on disk
			foreach (KeyValuePair<string, UserFile> entry in  userfilesystem.filemap) {
				string parentdir = GetParentDirectoryPath( entry.Key);
				string filepath = userpath + "files" + Path.DirectorySeparatorChar;
				string metadatapath = userpath + "metadata" + Path.DirectorySeparatorChar;
				Directory.CreateDirectory(filepath + parentdir);
				Directory.CreateDirectory(metadatapath + parentdir);

				string completefilepath = filepath + entry.Key;
				string completemetadatafilepath = metadatapath + entry.Key + ".dat";
				System.IO.File.WriteAllText( completemetadatafilepath, entry.Value.GenerateMetaDataStringFromFile());
				File.WriteAllBytes( completefilepath, entry.Value.ReadFileContent());
			}
		}

 
		private string GetParentDirectoryPath( string fullpath){
			return Path.GetDirectoryName(fullpath) + Path.DirectorySeparatorChar;
		}

		private string GenerateCheckpointPath(DateTime checkpointtime){
			DateTime time = checkpointtime;
			string path = time.Year + "-" + time.Month + "-" + time.Day + Path.DirectorySeparatorChar + 
				time.Hour + "-" + time.Minute + Path.DirectorySeparatorChar;
			return this.pathprefix + path;
		}
	}
}


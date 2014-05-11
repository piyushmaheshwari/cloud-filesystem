using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Web;
using System.Web.SessionState;
using ServiceStack;

//author - piyush
namespace persistentbackend
{	
	[Route("/doCheckPoint", "POST")]
	public class DoCheckPoint
	{
		public CheckPointObject checkPointObject  { get; set; }
	}

	[Route("/restoreCheckPoint", "GET")]
	public class RestoreCheckPoint : IReturn<CheckPointObject>{

	}

	public class PersistentStorageService : Service
	{
		private static readonly log4net.ILog logger = 
			log4net.LogManager.GetLogger(typeof(PersistentStorageService));

		public object Get (RestoreCheckPoint request)
		{
			try{
				logger.Debug("API call for restoring the check point");
				CheckPointObject obj =  new CheckpointLogic().RestoreFileSystem(true);
				return obj;
			} catch (Exception e) {
				logger.Debug(e);
				throw e;
			}
		}

		public void Post (DoCheckPoint request)
		{
			try {
				logger.Debug ("API Request received for checkpointing ");
				if (request.checkPointObject == null) {
					logger.Warn ("DAFUQ");
				}
				new CheckpointLogic ().DoCheckPointAllUsers (request.checkPointObject);

			} catch (Exception e) {
				logger.Debug(e);
				throw e;
			}
		}


	}
}

// ------------------------------------------------------------------------------
// <auto-generated>
//    Generated by RoboKindChat.vshost.exe, version 0.9.0.0
//    Changes to this file may cause incorrect behavior and will be lost if code
//    is regenerated
// </auto-generated>
// ------------------------------------------------------------------------------
namespace org.cogchar.bind.cogbot.avrogen
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Avro;
	using Avro.Specific;
	
	public interface InterpreterSuccess
	{
		Schema Schema
		{
			get;
		}
		string sourceId
		{
			get;
		}
		string destinationId
		{
			get;
		}
		long timestampMillisecUTC
		{
			get;
		}
		string message
		{
			get;
		}
		string instanceLoadId
		{
			get;
		}
		string requestId
		{
			get;
		}
		string valueId
		{
			get;
		}
	}
}

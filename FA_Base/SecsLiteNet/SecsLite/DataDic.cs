using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecsLite
{

	/// <summary>
	/// 20250108  liyb  根据SEMI E05 文档整合常见的数据项，对比之前的数据项数量有所补充，
	/// 删除掉之前已有数据项的枚举单独文件，将其对应的枚举定义在此文件中，简化项目结构
	/// </summary>
	/*
	Binnary            Octal    Meaning SML           notation

	000000             00       LIST（元素长度）                L
	001000             10       二进制                          B
	001001             11       布尔型                          BOOLEAN
	010000             20       ASCII                           A
	010001             21       JIS-8                           J
	010010             22       2-byte character                J2
	011000             30       8-byte integer（ signed）       I8
	011001             31       1-byte interger（ signed）      I1
	011010             32       2-byte interger（ signed）      I2
	011100             34       4-byte interger（ signed）      I4
	100000             40       8-byte float point F8
	100100             44       4-byte float point F4
	101000             50       8-byte integer（ unsigned）     U8
	101001             51       1-byte integer（ unsigned）     U1
	101010             52       2-byte integer（ unsigned）     U2
	101100             54       4-byte integer（ unsigned）     U4*/
	public enum GemResult
	{
		Success,
		HsmsFailed,
		NotImplemented,
		UnhandledMessage,
		UndefinedEventID,
		UndefinedEventName,
		UndefinedReportID,
		UndefinedAlarmID,
		UndefinedAlarmName,
		AlarmDisabled,
		ReadEquipmentValError,
		AlreadyExist,
		SwitchDenied,
		FileNotFound,
		FormatError,
		ConfigFileError,
		InvalidParameter,
		NameDuplicated
	}
	public enum ALCDCode : byte
	{
		NotUsed,
		PersonalSafety,
		EquipmentSafety,
		ParameterControlWarning,
		ParameterControlError,
		IrrecoverableError,
		EquipmentStatusWarning,
		AttentionFlags,
		DataIntegrity
	}

	public enum ControlStateEnum : byte
    {
		Online,
		Offline
    }

	public enum SecsFormat
	{
		List = 0,
		Binary = 32,
		Boolean = 36,
		Ascii = 64,
		JIS8 = 68,
		DBytes = 72,
		I8 = 96,
		I1 = 100,
		I2 = 104,
		I4 = 112,
		F8 = 128,
		F4 = 144,
		U8 = 160,
		U1 = 164,
		U2 = 168,
		U4 = 176,
		Unknown = 255
	}

	public enum HsmsMode
	{
		Active = 1,
		Passive
	}

	public enum  SECSIMode
	{
		MASTER = 0,               ///< Master模式（主动发起请求）
         SLAVE = 1                 ///< Slave模式（被动响应）
    }

	public enum SecsErrorCode
	{
		Success = 0,
		ErrorDeviceId = -1,
		ErrorStreamType = -3,
		ErrorFunctionType = -5,
		ErrorIllegalData = -7,
		ErrorTransTimeout = -9,
		ErrorDataTooLong = -11
	}

	public enum SecsResult
	{
		Success,
		ConfigError,
		StartPassiveFailed,
		ConnectFailed,
		ModeMismatch,
		UnhandledError
	}

	public enum SecsSendFlag
	{
		NeedReply = 1
	}

	#region 数据项 A-C

	/// <summary>
	/// Format 11 表明请求成功    TRUE is successful else FALSE
	/// </summary>
	public enum ACKA : byte
	{
		Accepted = 0,
		Denied = 1 
	}

	/// <summary>
	/// format 10   (Acknowledge Code)确认码， 1 byte
	/// </summary>
	public enum ACKC5 : byte
	{
		Accepted, //接受
		Denied   //错误，不接受
	}

	/// <summary>
	/// format 10   (Acknowledge Code)确认码， 1 byte
	/// </summary>
	public enum ACKC6 : byte
	{
		Accepted, //接受
		Denied    //错误，不接受
	}


	/// <summary>
	/// format： 10  Acknowledge code, 1 byte
	/// </summary>
	public enum ACKC7 : byte
	{
		Accepted,
		PermissionNotGranted,
		LengthError,
		MatrixOverflow,
		PPIDNotFound,
		ModeUpsupported
	}

	/// <summary>
	/// format： 31, 51    Acknowledge Code, 1 byte
	/// </summary>
	public enum ACKC7A : byte
	{
		Accepted,
		MDLNIsInconsistent,
		SOFTREVIsInconsistent,
		InvalidCCODE,
	    InvalidPPARMValue,
		OtherError,
	}


	/// <summary>
	/// format： 10  Acknowledge code, 1 byte
	/// </summary>
	public enum ACKC10 : byte
	{
		AcceptedForDisplay,    //接受显示
		MsgNotDisplayed,       //消息没有显示
		TerminalNotAvailable  //终端不可用
	}

	/// <summary>
	/// format： 10  Return code for secondary messages 1 byte.  (回复消息的返回码)
	/// </summary>
	public enum ACKC13 : byte
	{
		OK,
		TryLater,                //稍后尝试
		UnkonwnDataSetName,      //错误： 未知的数据集名称
		IllegalCheckpointValue,  //错误： 非法检查点的值
		TooManyOpenDataSets,     //错误：打开太多的数据集
		DataSetOpenTooManyTimes, //错误：数据集打开太多次
		NoOpenDataSet,           //错误：没有打开的数据集
		CannotContinue,          //错误：不能继续
		ErrorEndOfData,          //错误：数据尾
		HandleInUse,             //错误：句柄被使用
		PendingTransaction       //错误：未知的传输
	}

	/// <summary>
	/// format： 10  (Alarm Code byte)警报码，只有 bit 8 使用  bit 8 = 1 警报设置  bit8 = 0 警报清除
	/// </summary>
	public enum ALCD : byte
	{
		Cleared = 0,
		Set = 0x80
	}

	/// <summary>
	/// format： 10  Alarm enable/disable code, 1 byte  bit 8 = 1 means enable alarm   bit 8 = 0 means disable alarm
	/// </summary>
	public enum ALED : byte
	{
		Disable = 0,
		Enable = 0x80
	}

	/// <summary>
	/// format： 20,5()    指定的 qualifying value 与对象实例的属性值之间的关系。
	/// </summary>
	public enum ATTRRELN : byte
	{
		Equal,
		NotEqual,
		LessThan,
		LessThanOrEqual,
		GreaterThan,
		GreaterThanOrEqual,
		Present,
		Absent
	}

	/// <summary>
	/// format 51  carrier 动作确认码， 1 byte
	/// </summary>
	public enum CAACK : byte
	{
		Acknowledge,
		InvalidCommand,
		CanNotPerformNow,
		InvalidDataOrArgument,
		AcknowledgeCompletionLater,
		Rejected,
		CommandPerformedWithErrors
	}


	/// <summary>
	/// format 11 (collection event or trace enable/disable code)事件或 trace 使能码， 1 byte
	/// </summary>
	public enum CEED : byte
	{
		False = 0,
        True = 1
	}

	/// <summary>
	/// format 0,51
	/// </summary>
	public enum CEPACK : byte
	{
		NoError,
		CPNAMENotExit,
		IllegalValueSpecifiedForCEPVAL,
		IllegalFormatSpecifiedForCEPVAL,
		CPNAMNotValidAsUsed
	}

	/// <summary>
	/// format 31，51  Command acknowledge code
	/// </summary>
	public enum CMDA : byte
	{
		CompletedOrDone,
		CommandNotExit,
		CannotPerformNow,
		OtherEquipmentSpecificError
	}


	/// <summary>
	/// format： 10
	//描述： (Establish Communications Acknowledge Code)建立通信应答， 1 byte
	/// </summary>
	public enum COMMACK : byte
	{
		Accepted,
		Denied
	}

	/// <summary>
	/// format： 10 描述： (Command Parameter Acknowledge Code)命令参数确认码， 1 byte
	/// </summary>
	public enum CPACK : byte
	{
		CPNAMENotExit,
		IllegalValueSpecifiedForCPVAL,
		IllegalFormatSpecifiedForCPVAL,
		OtherEquipmentSpecificError
	}

	/// <summary>
	/// Equipment Acknowledgement code, 1 byte
	/// </summary>
	public enum CSAACK : byte
	{
		EverythingCorrect,
		Busy,
		InvalidSPID,
		InvalidData,
		EquipmentSpecificError
	}

	#endregion


	#region 数据项 D-F

	/// <summary>
	/// format： 10
	/// 描述： (Define Report Acknowledge Code ) 定义报告确认码， 1 byte
	/// </summary>
	public enum DRACK : byte
	{
		Accept,
		InsufficientSpace,
		InvalidFormat,
		ReportIDDefined,
		VIDNotExist
	}

	/// <summary>
	///format10 Equipment acknowledge code, 1 byte
	///  </summary>
	public enum EAC : byte
	{
		Acknowledge,
		DeniedECIDNotExist,
		DeniedBusy,
		DeniedOutOfRange
	}

	/// <summary>
	/// format： 10
	///描述： (Enable/Disable Event Report Acknowledge Code)使能事件报告确认， 1 byte
	/// </summary>
	public enum ERACK : byte
	{
		Accepted,
		DeniedCEIDNotExist,
	}

	/// <summary>
	/// format： 5()
	//描述： 错误码识别
	/// </summary>
	public enum ERRCODE : uint
	{
		NoError = 0u,
		UnknownObjectInSpecifier = 1u,
		UnknownTargetObjectType = 2u,
		UnknownObjectInstance = 3u,
		UnknownAttributeName = 4u,
		ReadOnlyAttributeAccessDenied = 5u,
		UnknownObjectType = 6u,
		InvalidAttributeValue = 7u,
		SyntaxError = 8u,
		VerificationError = 9u,
		ValidationError = 10u,
		ObjectIdentifierInUse = 11u,
		ParametersImproperlySpecified = 12u,
		InsufficientParametersSpecified = 13u,
		UnsupportedOptionRequested = 14u,
		Busy = 15u,
		NotAvailableForProcessing = 16u,
		CommandNotValidForCurrentState = 17u,
		NoMaterialAltered = 18u,
		MaterialPartiallyProcessed = 19u,
		AllMaterialProcessed = 20u,
		RecipeSpecificationRelatedError = 21u,
		FailedDuringProcessing = 22u,
		FailedDuringNotProcessing = 23u,
		FailedDueToLackOfMaterial = 24u,
		JobAborted = 25u,
		JobStopped = 26u,
		JobCancelled = 27u,
		CannotChangeSelectedRecipe = 28u,
		UnknownEvent = 29u,
		DuplicateReportID = 30u,
		UnknownDataReport = 31u,
		DataReportNotLinked = 32u,
		UnknownTraceReport = 33u,
		DuplicateTraceID = 34u,
		TooManyDataReports = 35u,
		SamplePeriodOutOfRange = 36u,
		GroupSizeTooLarge = 37u,
		RecoveryActionCurrentlyInvalid = 38u,
		BusyWithAnotherRecoveryCurrentlyUnableToPerformTheRecovery = 39u,
		NoActiveRecoveryAction = 40u,
		ExceptionRecoveryFailed = 41u,
		ExceptionRecoveryAborted = 42u,
		InvalidTableElement = 43u,
		UnknownTableElement = 44u,
		CannotDeletePredefined = 45u,
		InvalidToken = 46u,
		InvalidParameter = 47u,
		CarrierAreadyInUse=48u,
		VerificationTimeOut=49u,
	}
	#endregion


	#region 数据项 G-K

	/// <summary>
	/// format： 10
	///描述： Grant Code, 1 byte
	/// </summary>
	public enum GRANT : byte
	{
		PermissionGranted,
		BusyTryAgain,
		NoSpaceAvailable,
		DuplicateDATAID
	}

	/// <summary>
	/// format： 10
	///描述：（ Permission to send）允许发送， 1byte
	/// </summary>
	public enum GRANT6 : byte
	{
		PermissionGranted,
		BusyTryAgain,
		NotiInterested
	}


	/// <summary>
	/// format： 10   Grant code, 1 byte
	/// </summary>
	public enum GRNT1 : byte
	{
		PositiveResponseTransferOK,
		BusyTryAgain,
		NoSpace,
		MapTooLarge,
		DuplicateID,
		MaterialIdNotFound,
		UnknownMapFormat,
		Error
	}


	/// <summary>
	/// format： 10
	/// 描述： (Host Command Parameter Acknowledge Code)host 命令参数确认码 ， 1 byte
	/// </summary>
	public enum HCACK : byte
	{
		Acknowledge,
		CommandNotExist,
		CantPerformNow,
		ParamInvalid,
		PerformLater,
		RejectAlreadyInCondition,
		NoSuchObjectExists
	}

	/// <summary>
	/// format51  Tells whether the cancel ready message was accepted or rejected
	/// </summary>
	public enum HOCANCELACK : byte
	{
		CancelReadyAccepted,
		AtomicTransferUnknown,
		CancelReadyRejected
	}

	/// <summary>
	/// Tells whether the halt command was accepted or rejected
	/// </summary>
	public enum HOHALTACK : byte
	{
		HaltAccepted,
		AtomicTransferUnknown,
	}

	/// <summary>
	/// format 10
	/// </summary>
	public enum IDTYP : byte
	{
		WaferId,
		WaferCassetteId,
		FilmFrameID,
		Error
	}


	#endregion


	#region 数据项 L-M

	/// <summary>
	/// format： 10
	/// 描述： 变量边界属性设置的确认码， 1 byte。
	/// </summary>
	public enum LIMITACK : byte
	{
		LIMITIDNotExist=1,
		UPPERDBGreaterThanLIMITMAX=2,
		LOWERDBLessThanLIMITMIN=3,
		UPPERDBLessThanLOWERDB=4,
		IllegalFormatSpecified=5,
		ASCIIValueCannotBeTranslatedToNumeric=6,
		DuplicateLimitDefinitionVariable=7,
	}

	/// <summary>
	/// format： 10
	/// 描述： (Link Report Acknowledge Code)关联结果回馈 ,1 byte
	/// </summary>
	public enum LRACK : byte
	{
		Accepted,
		InsufficientSpace,
		InvalidFormat,
		CEIDDefined,
		CEIDNotExist,
		RPTIDNotExist
	}

	/// <summary>
	/// format： 10
	/// 描述： 变量边界定义确认码， 1 byte。定义 VID 边界属性的错误。
	/// </summary>
	public enum LVACK : byte
	{
		VariableNotExist=1,
		VariableNoLimitsCapability=2,
		VariableRepeated=3,
		LIMITACKLimitValueError=4
	}


	/// <summary>
	/// format 10  Map Error
	/// </summary>
	public enum MAPER : byte
	{
		IDNotFound,
		InvalidData,
		FormatError,
		InvalidError
	}

	/// <summary>
	/// format 10  Map data format type
	/// </summary>
	public enum MAPFT : byte
	{
		RowFormat,
		ArrayFormat,
		CoordinateFormat,
		Error
	}

	/// <summary>
	/// format 10 Map data acknowledge
	/// </summary>
	public enum MDACK : byte
	{
		MapReceived,
		FormatError,
		NoIDMatch,
		AbortOrDiscardMap,
		Error
	}

	/// <summary>
	/// format： 10,20
	/// 描述： (Material format) 物料格式， 1byte。
	/// </summary>
	public enum MaterialFormat : byte
	{
		QuantitiesInWafers = 1,
		QuantitiesInCassette,
		QuantitiesInDieOrChips,
		QuantitiesInBoats,
		QuantitiesInIngots,
		QuantitiesInLeadFrames,
		QuantitiesInLots,
		QuantitiesInMagazines,
		QuantitiesInPackages,
		QuantitiesInPlates,
		QuantitiesInTubes,
		QuantitiesInWaferframes,
		QuantitiesInCarriers,
		QuantitiesInSubstrates
	}

	/// <summary>
	/// format 10 Material ID Acknowledge Code, 1 byte
	/// </summary>
	public enum MIDAC : byte
	{
		Accepted,
		InvalidPortNumber,
		MaterialNotPresentAtIdentifiedPort,
		error
	}

	/// <summary>
	/// format 10 Material ID Acknowledge Code, 1 byte
	/// </summary>
	public enum MIDRA : byte
	{
		MIDFollows,
		NotSendMID,
		SendMIDLaterInS3F13
	}


	/// <summary>
	/// format 10  Matrix mode select, 1 byte
	/// </summary>
	public enum MMODE : byte
	{
		HostSourceMode,
		LocalSourceMode,
		HostImmediateMode
	}

	#endregion


	#region 数据项 N-P

	/// <summary>
	/// format： 51
	/// 描述：确认码
	/// </summary>
	public enum OBJACK : byte
	{
		Success,
		Error
	}

	/// <summary>
	/// Format: 10   Acknowledge code for OFF-LINE request.
	/// </summary>
	public enum OFLACK : byte
	{
		OFFLINEAcknowledge,
	}

	/// <summary>
	/// Format: 10  Acknowledge code for ON-LINE request
	/// </summary>
	public enum ONLACK : byte
	{
		Accepted,
		NotAllowed,
		OnlineAlready
	}

	public enum PPGNT : byte
	{
		OK = 0,
		AlreadyHave = 1,
		Nospace = 2,
		InvalidPPID = 3,
		BusyTryLater = 4,
		NotAccept = 5,
		OtherError
	}

	#endregion


	#region 数据项 Q-S

	/// <summary>
	/// format 10 (Request Spool Data Acknowledge)请求缓存数据确认。
	/// </summary>
	public enum RSDA : byte
	{
		OK,
		BusyTryLater,
		spooledDataNotExist
	}

	/// <summary>
	/// format 51 (Request Spool DataCode)请求缓存数据码
	/// </summary>
	public enum RSDC : byte
	{
		TransmitSpooledMessages,
		PurgeSpooledMessages
	}


	/// <summary>
	/// format： 10
	/// 描述： (reset spooling Acknowledge)缓存设置回馈
	/// </summary>
	public enum RSPACK : byte
	{
		SpoolingResetAccepted,
		spoolingResetRejected,
	}

	/// <summary>
	/// format 10 Map set-up data acknowledge
	/// </summary>
	public enum SDACK : byte
	{
		ReceivedData,
		Error,
	}

	/// <summary>
	/// format 10 Spool Stream Acknowledge
	/// </summary>
	public enum STRACK : byte
	{
		SpoolingNotAllowedForstream,
		StreamUnknown,
		UnknownFuncSpecifiedForstream,
		SecondaryFuncSpecifiedForstream
	}


	/// <summary>
	/// format： 10
	/// 描述：服务接受应答码。 1 byte
	/// </summary>
	public enum SVCACK : byte
	{
		Acknowledge,
		ServiceNotExist,
		CannotPerformNow,
		ParameterInvalid,
		AcknowledgeCompletionNotifiedLater,
		ServiceNotCompletedOrProhibited,
		NoSuchObjectExists
	}


	#endregion


	#region 数据项 T-Z

	public enum TimeFormat
	{
		ShortFormat,
		LongFormat
	}

	/// <summary>
	/// format： 10
	/// 描述： (Time Acknowledge Code)时间确认码， 1 byte
	/// </summary>
	public enum TIACK : byte
	{
		OK,
		Error
	}


	/// <summary>
	/// format： 10
	/// 描述： (Equipment acknowledgement code)设备确认码， 1 byte
	/// </summary>
	public enum TIAACK : byte
	{
		OK,
		TooManySVID,
		NoMoreTraceAllowed,
		InvalidPeriod,
		UnknownSVID,
		InvalidREPGSZ
	}

	/// <summary>
	/// format： 10
	/// 描述： 变量边界属性确认码， 1 byte。
	/// </summary>
	public enum VLAACK : byte
	{
		Acknowledge,
		LimitAttrDefineError,
		CannotPerformNow
	}
	#endregion
}

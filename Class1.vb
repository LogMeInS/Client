Imports System.IO
Imports System.Net.Sockets
Imports System.Text

Public Class LogMeIn
    Dim Options As Settings

    Dim Server As TcpClient

    Dim Writer As BinaryWriter
    Dim Reader As BinaryReader

    Dim Watchers As New List(Of FileSystemWatcher)

    Dim CurrentUser As User

    Event OnStartDownload(Count As Integer)
    Event OnFileDownloaded()

#Region "Constructor"
    Public Sub New(P As Settings)
        Options = P

        Server = New TcpClient(Options.ServerIp, Options.ServerPort)

        Writer = New BinaryWriter(Server.GetStream())
        Reader = New BinaryReader(Server.GetStream())
    End Sub
#End Region

#Region "Logic"
    Sub Initaliaze()
        Dim Files As String() = GetFilesList()

        RaiseEvent OnStartDownload(Files.Length)

        'If Files(0) <> "null" Then
        For Each File As String In Files
            DownloadFile(File)

            RaiseEvent OnFileDownloaded()
        Next
        'End If
        CreateWatchers()
    End Sub

    Sub Logout()
        SendMessage("DeleteFiles")

        Dim Files As New List(Of String)

        For Each Directory In Options.Paths
            For Each File In IO.Directory.GetFiles(Directory.Value)
                Files.Add(File)
            Next
        Next
        For i As Integer = 0 To Files.Count - 1
            For Each P As KeyValuePair(Of String, String) In Options.Paths
                Files(i) = Files(i).Replace(P.Value, P.Key)
            Next
        Next

        SendMessage(Serialization.Serialize(Files))
    End Sub

    Sub CreateWatchers()
        For Each P As KeyValuePair(Of String, String) In Options.Paths
            Dim Watcher As New FileSystemWatcher()

            Watcher.IncludeSubdirectories = True

            Watcher.Path = P.Value

            Watcher.NotifyFilter = NotifyFilters.LastAccess Or NotifyFilters.LastWrite Or NotifyFilters.FileName Or NotifyFilters.DirectoryName

            AddHandler Watcher.Changed, AddressOf OnChanged
            AddHandler Watcher.Created, AddressOf OnChanged
            AddHandler Watcher.Deleted, AddressOf OnChanged
            AddHandler Watcher.Renamed, AddressOf OnChanged

            Watcher.EnableRaisingEvents = True

            Watchers.Add(Watcher)
        Next
    End Sub

    Function GetName() As String
        SendMessage("GetName")
        Return ReceiveMessage()
    End Function

    Function GetPicture() As Byte()
        SendMessage("GetPicture")
        Return ReceiveFile()
    End Function

    Function Register(Username, Password, Name, Surname, UserClass, UserParallel) As Boolean
        SendMessage("CreateUser")

        SendMessage(Username)
        SendMessage(Password)
        SendMessage(Name)
        SendMessage(Surname)
        SendMessage(UserClass)
        SendMessage(UserParallel)

        Return If(ReceiveMessage() = "Y", True, False)
    End Function

    Function Login(Username, Password) As Boolean
        SendMessage("LoginUser")

        SendMessage(Username)
        SendMessage(Password)

        If ReceiveMessage() = "Y" Then
            CurrentUser = Serialization.Deserialize(ReceiveMessage(), GetType(User))

            Initaliaze()

            Return True
        Else Return False
        End If
    End Function

    Function GetFilesList() As String()
        SendMessage("GetFilesList")

        Return Serialization.Deserialize(ReceiveMessage(), GetType(String()))
    End Function

    Sub DownloadFile(RelativePath As String)
        SendMessage("DownloadFile")
        SendMessage(RelativePath)

        Dim File As Byte() = ReceiveFile()

        Dim Path As String = RelativePath

        For Each P As KeyValuePair(Of String, String) In Options.Paths
            Path = Path.Replace(P.Key, P.Value)
        Next

        If Not IO.Directory.Exists(IO.Path.GetDirectoryName(Path)) Then
            IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(Path))
        End If

        IO.File.WriteAllBytes(Path, File)
    End Sub

    Sub UploadFile(Path As String)
        Dim RelativePath As String = Nothing

        For Each P As KeyValuePair(Of String, String) In Options.Paths
            RelativePath = Path.Replace(P.Value, P.Key)
        Next

        SendMessage("UploadFile")
        SendMessage(RelativePath)
        SendFile(IO.File.ReadAllBytes(Path))
    End Sub
#End Region

#Region "Other"
    Sub OnChanged(sender As Object, e As FileSystemEventArgs)
        If IO.File.Exists(e.FullPath) Then UploadFile(e.FullPath)
    End Sub
#End Region

#Region "Messaging"
    Sub SendMessage(message As String)
        Dim bytes As Byte() = Encoding.Unicode.GetBytes(message)

        Writer.Write(bytes.Length)

        Writer.Write(bytes)
    End Sub
    Function ReceiveMessage() As String
        Dim messageLength As Integer = Reader.ReadInt32
        Dim messageData() As Byte = Reader.ReadBytes(messageLength)
        Dim message As String = Encoding.Unicode.GetString(messageData)

        Return message
    End Function

    Sub SendFile(file As Byte())
        Dim bytes As Byte() = file

        Writer.Write(bytes.Length)

        Writer.Write(bytes)
    End Sub
    Function ReceiveFile() As Byte()
        Dim fileLength As Integer = Reader.ReadInt32
        Dim file() As Byte = Reader.ReadBytes(fileLength)

        Return file
    End Function
#End Region

    Class Serialization
        Public Shared Function Serialize(Obj As Object) As String
            Dim xs As New System.Xml.Serialization.XmlSerializer(Obj.GetType)
            Dim w As New IO.StringWriter()
            xs.Serialize(w, Obj)

            Return w.ToString
        End Function

        Public Shared Function Deserialize(Xml As String, T As Type) As Object
            Dim serializer As New System.Xml.Serialization.XmlSerializer(T)
            Using r As TextReader = New StringReader(Xml)
                Return serializer.Deserialize(r)
            End Using
        End Function
    End Class

    <Serializable> Public Class User
#Region "Fields"
        Public Username As String
        Public Password As String
        Public Name As String
        Public Surname As String
        Public UserClass As String
        Public UserParallel As String

        Public ID As ULong
#End Region

#Region "Constructors"
        Public Sub New()
        End Sub
#End Region

        Public Overrides Function Equals(obj As Object) As Boolean
            Return obj.Username = Username And obj.Password = Password
        End Function
    End Class

    Class Settings
        Public ServerIp As String
        Public ServerPort As Integer

        Public Paths As Dictionary(Of String, String)
    End Class
End Class

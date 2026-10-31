Public Class PageToolsLeft

    ''' <summary>当前页面的编号。</summary>
    Public PageID As FormMain.PageSubType
    Public Sub New()
        InitializeComponent()
        PageID = FormMain.PageSubType.OtherColorText
        ItemColorText.Checked = True
    End Sub

    Private Sub PageCheck(sender As FrameworkElement, e As RouteEventArgs) Handles ItemMore.Check, ItemColorText.Check, ItemOreGuide.Check, ItemGameStats.Check, ItemCommandQuery.Check
        If sender.Tag IsNot Nothing Then PageChange(Val(sender.Tag))
    End Sub

    ''' <summary>从其他页面（如主页「游玩洞察」）跳进来时，把左侧选中项同步到目标工具。</summary>
    Public Sub SyncChecked(ID As FormMain.PageSubType)
        '未知编号（含 -1）忽略：保持当前选中项
        Select Case ID
            Case FormMain.PageSubType.OtherTools, FormMain.PageSubType.OtherColorText,
                 FormMain.PageSubType.OtherOreGuide, FormMain.PageSubType.OtherGameStats,
                 FormMain.PageSubType.OtherCommandQuery
            Case Else
                Return
        End Select
        If PageID = ID Then Return
        PageID = ID
        Select Case ID
            Case FormMain.PageSubType.OtherTools
                ItemMore.Checked = True
            Case FormMain.PageSubType.OtherColorText
                ItemColorText.Checked = True
            Case FormMain.PageSubType.OtherOreGuide
                ItemOreGuide.Checked = True
            Case FormMain.PageSubType.OtherGameStats
                ItemGameStats.Checked = True
            Case FormMain.PageSubType.OtherCommandQuery
                ItemCommandQuery.Checked = True
        End Select
    End Sub

    Public Function PageGet(Optional ID As FormMain.PageSubType = -1)
        If ID = -1 Then ID = PageID
        Select Case ID
            Case FormMain.PageSubType.OtherTools
                If FrmOtherTools Is Nothing Then FrmOtherTools = New PageOtherTools
                Return FrmOtherTools
            Case FormMain.PageSubType.OtherColorText
                If FrmColorText Is Nothing Then FrmColorText = New PageColorText
                Return FrmColorText
            Case FormMain.PageSubType.OtherOreGuide
                If FrmOreGuide Is Nothing Then FrmOreGuide = New PageOreGuide
                Return FrmOreGuide
            Case FormMain.PageSubType.OtherGameStats
                If FrmGameStats Is Nothing Then FrmGameStats = New PageGameStats
                Return FrmGameStats
            Case FormMain.PageSubType.OtherCommandQuery
                If FrmCommandQuery Is Nothing Then FrmCommandQuery = New PageCommandQuery
                Return FrmCommandQuery
            Case Else
                Throw New Exception("未知的工具箱子页面种类：" & ID)
        End Select
    End Function

    Public Sub PageChange(ID As FormMain.PageSubType)
        If PageID = ID Then Return
        AniControlEnabled += 1
        Try
            PageChangeRun(PageGet(ID))
            PageID = ID
        Catch ex As Exception
            Logger.Error(ex, $"切换工具箱子页面失败（ID {ID}）")
        Finally
            AniControlEnabled -= 1
        End Try
    End Sub

    Private Shared Sub PageChangeRun(Target As MyPageRight)
        AniStop("FrmMain PageChangeRight")
        If Target.Parent IsNot Nothing Then Target.SetValue(ContentPresenter.ContentProperty, Nothing)
        FrmMain.PageRight = Target
        CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnExit()
        AniStart({
                         AaCode(Sub()
                                    CType(FrmMain.PanMainRight.Child, MyPageRight).PageOnForceExit()
                                    FrmMain.PanMainRight.Child = FrmMain.PageRight
                                    FrmMain.PageRight.Opacity = 0
                                End Sub, 130),
                         AaCode(Sub()
                                    FrmMain.PageRight.Opacity = 1
                                    FrmMain.PageRight.PageOnEnter()
                                End Sub, 30, True)
                 }, "PageLeft PageChange")
    End Sub

End Class

Imports System

Module Program
    Sub Main(args As String())
        With "hello"
            With .Replace("h", "y")
                With .Replace("o", "ow")
                    Dim x = .ToString()
                    Console.WriteLine(x)
                End With
            End With
        End With
    End Sub
End Module

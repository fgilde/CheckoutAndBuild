using System;
using System.Xml;
using Microsoft.Build.Shared;

internal sealed class BuildEventFileInfo
{
    private string file;
    private int line;
    private int column;
    private int endLine;
    private int endColumn;

    internal string File
    {
        get
        {
            return this.file;
        }
    }

    internal int Line
    {
        get
        {
            return this.line;
        }
    }

    internal int Column
    {
        get
        {
            return this.column;
        }
    }

    internal int EndLine
    {
        get
        {
            return this.endLine;
        }
    }

    internal int EndColumn
    {
        get
        {
            return this.endColumn;
        }
    }

    private BuildEventFileInfo()
    {
    }

    internal BuildEventFileInfo(string file)
      : this(file, 0, 0, 0, 0)
    {
    }

    //internal BuildEventFileInfo(IElementLocation location)
    //  : this(location.File, location.Line, location.Column)
    //{
    //}

    internal BuildEventFileInfo(string file, int line, int column)
      : this(file, line, column, 0, 0)
    {
    }

    internal BuildEventFileInfo(string file, int line, int column, int endLine, int endColumn)
    {
        this.file = file == null ? string.Empty : file;
        this.line = line;
        this.column = column;
        this.endLine = endLine;
        this.endColumn = endColumn;
    }

    internal BuildEventFileInfo(XmlException e)
    {
        this.file = e.SourceUri.Length == 0 ? string.Empty : new Uri(e.SourceUri).LocalPath;
        this.line = e.LineNumber;
        this.column = e.LinePosition;
        this.endLine = 0;
        this.endColumn = 0;
    }
}
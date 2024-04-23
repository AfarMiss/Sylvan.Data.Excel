using Sylvan.Data.Excel.Xlsx;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sylvan.Data.Excel;

/// <summary>
/// Writes data to excel files.
/// </summary>
public abstract class ExcelDataWriter : IDisposable
{
	private protected class SharedStringTable
	{
		readonly Dictionary<SharedStringEntry, string> dict;
		readonly List<SharedStringEntry> entries;

		public int UniqueCount => entries.Count;

		public string this[int idx] => entries[idx].str;

		public SharedStringTable()
		{
			const int InitialSize = 128;
			this.dict = new Dictionary<SharedStringEntry, string>(InitialSize);
			this.entries = new List<SharedStringEntry>(InitialSize);
		}

		struct SharedStringEntry : IEquatable<SharedStringEntry>
		{
			public string str;
			public string idxStr;

			public SharedStringEntry(string str)
			{
				this.str = str;
				this.idxStr = "";
			}

			public override int GetHashCode()
			{
				return str.GetHashCode();
			}

			public override bool Equals(object? obj)
			{
				return obj is SharedStringEntry e && this.Equals(e);
			}

			public bool Equals(SharedStringEntry other)
			{
				return this.str.Equals(other.str);
			}
		}

		public string GetString(string str)
		{
			var entry = new SharedStringEntry(str);
			string? idxStr;
			if (!dict.TryGetValue(entry, out idxStr))
			{
				idxStr = this.entries.Count.ToString();
				this.entries.Add(entry);
				this.dict.Add(entry, idxStr);
			}
			return idxStr;
		}
	}

	bool ownsStream;
	readonly Stream stream; 
	private protected readonly bool truncateStrings;

    private protected SharedStringTable sharedStrings;

	/// <summary>
	/// Creates a new ExcelDataWriter.
	/// </summary>
	public static ExcelDataWriter Create(string file, ExcelDataWriterOptions? options = null)
	{
		var fileStream = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1);
		try
		{
			var writer = Create(fileStream);
			writer.ownsStream = true;
			return writer;
		}
		catch (Exception)
		{
			fileStream?.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Creates a new ExcelDataWriter.
	/// </summary>
	public static ExcelDataWriter Create(Stream stream, ExcelDataWriterOptions? options = null)
	{
		options = options ?? ExcelDataWriterOptions.Default;
		return new XlsxDataWriter(stream, options);
	}

	/// <inheritdoc/>
	public virtual void Dispose()
	{
		if (ownsStream)
			this.stream.Dispose();
	}

	private protected ExcelDataWriter(Stream stream, ExcelDataWriterOptions options)
	{
		this.stream = stream;
		this.sharedStrings = new SharedStringTable();
		this.truncateStrings = options.TruncateStrings;
	}

	/// <summary>
	/// Writes data to a new worksheet with the given name.
	/// </summary>
	/// <returns>The number of rows written.</returns>
	public abstract WriteResult Write(DbDataReader data, string? worksheetName = null);

	/// <summary>
	/// Writes data to a new worksheet with the given name.
	/// </summary>
	/// <returns>The number of rows written.</returns>
	public abstract Task<WriteResult> WriteAsync(DbDataReader data, string? worksheetName = null, CancellationToken cancel = default);

	/// <summary>
	/// A value indicating the result of the write operation.
	/// </summary>
	public readonly struct WriteResult
	{
		readonly int value;

		internal WriteResult(int val, bool complete)
		{
			this.value = complete ? val : -val;
		}

		/// <summary>
		/// Gets the number of rows written.
		/// </summary>
		public int RowsWritten => value < 0 ? -value : value;

		/// <summary>
		/// Indicates if all rows from the 
		/// </summary>
		public bool IsComplete => value >= 0;
	}
}

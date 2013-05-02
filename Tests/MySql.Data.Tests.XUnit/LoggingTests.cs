﻿// Copyright © 2013 Oracle and/or its affiliates. All rights reserved.
//
// MySQL Connector/NET is licensed under the terms of the GPLv2
// <http://www.gnu.org/licenses/old-licenses/gpl-2.0.html>, like most 
// MySQL Connectors. There are special exceptions to the terms and 
// conditions of the GPLv2 as it is applied to this software, see the 
// FLOSS License Exception
// <http://www.mysql.com/about/legal/licensing/foss-exception.html>.
//
// This program is free software; you can redistribute it and/or modify 
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation; version 2 of the License.
//
// This program is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License 
// for more details.
//
// You should have received a copy of the GNU General Public License along 
// with this program; if not, write to the Free Software Foundation, Inc., 
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using System.Diagnostics;

namespace MySql.Data.MySqlClient.Tests.XUnit
{
  public class LoggingTests : IUseFixture<SetUpClass>, IDisposable
  {
    private SetUpClass st;
    private string connString = string.Empty;

    public string conn
    {      
       get { 
        connString = st.conn.ConnectionString;
        connString += st.csAdditions;
        return connString;
      }
    }

    public void SetFixture(SetUpClass data)
    {      
      st = data;
      st.csAdditions = ";logging=True;";      
      st.createTable("CREATE TABLE Test (id int, name VARCHAR(200))", "INNODB");      
    }

    public void Dispose()
    {
      st.execSQL("DROP TABLE IF EXISTS TEST");
    }

    [Fact]
    public void SimpleLogging()
    {
      st.execSQL("INSERT INTO Test VALUES (1, 'Test1')");
      st.execSQL("INSERT INTO Test VALUES (2, 'Test2')");
      st.execSQL("INSERT INTO Test VALUES (3, 'Test3')");
      st.execSQL("INSERT INTO Test VALUES (4, 'Test4')");

      MySqlTrace.Listeners.Clear();
      MySqlTrace.Switch.Level = SourceLevels.All;
      GenericListener listener = new GenericListener();
      MySqlTrace.Listeners.Add(listener);

      using (MySqlConnection logConn = new MySqlConnection(this.conn))
      {
        logConn.Open();
        MySqlCommand cmd = new MySqlCommand("SELECT * FROM Test", logConn);
        using (MySqlDataReader reader = cmd.ExecuteReader())
        {
        }
      }
      //Assert.Equal(4, listener.Strings.Count);
      Assert.Equal(23, listener.Strings.Count);
      Assert.True(listener.Strings[listener.Strings.Count - 5].Contains("Query Opened: SELECT * FROM Test"));
      Assert.True(listener.Strings[listener.Strings.Count - 4].Contains("Resultset Opened: field(s) = 2, affected rows = -1, inserted id = -1"));
      Assert.True(listener.Strings[listener.Strings.Count - 3].Contains("Resultset Closed. Total rows=4, skipped rows=4, size (bytes)=32"));
      Assert.True(listener.Strings[listener.Strings.Count - 2].Contains("Query Closed"));
    }

    [Fact]
    public void Warnings()
    {
      st.execSQL("DROP TABLE IF EXISTS Test");
      st.execSQL("CREATE TABLE Test(id INT, name VARCHAR(5))");

      MySqlTrace.Listeners.Clear();
      MySqlTrace.Switch.Level = SourceLevels.All;
      GenericListener listener = new GenericListener();
      MySqlTrace.Listeners.Add(listener);

      using (MySqlConnection logConnection = new MySqlConnection(this.conn))
      {
        logConnection.Open();
        MySqlCommand cmd = new MySqlCommand("INSERT IGNORE INTO Test VALUES (1, 'abcdef')", logConnection);
        cmd.ExecuteNonQuery();
      }

      Assert.Equal(28, listener.Strings.Count);
      Assert.True(listener.Strings[listener.Strings.Count - 10].Contains("Query Opened: INSERT IGNORE INTO Test VALUES (1, 'abcdef')"));
      Assert.True(listener.Strings[listener.Strings.Count - 9].Contains("Resultset Opened: field(s) = 0, affected rows = 1, inserted id = 0"));
      Assert.True(listener.Strings[listener.Strings.Count - 8].Contains("Resultset Closed. Total rows=0, skipped rows=0, size (bytes)=0"));
      Assert.True(listener.Strings[listener.Strings.Count - 7].Contains("Query Opened: SHOW WARNINGS"));
      Assert.True(listener.Strings[listener.Strings.Count - 6].Contains("Resultset Opened: field(s) = 3, affected rows = -1, inserted id = -1"));
      Assert.True(listener.Strings[listener.Strings.Count - 5].Contains("Resultset Closed. Total rows=1, skipped rows=0, size (bytes)=55"));
      Assert.True(listener.Strings[listener.Strings.Count - 4].Contains("Query Closed"));
      Assert.True(listener.Strings[listener.Strings.Count - 3].Contains("MySql Warning: Level=Warning, Code=1265, Message=Data truncated for column 'name' at row 1"));
      Assert.True(listener.Strings[listener.Strings.Count - 2].Contains("Query Closed"));
    }

    [Fact]
    public void ProviderNormalizingQuery()
    {
      MySqlTrace.Listeners.Clear();
      MySqlTrace.Switch.Level = SourceLevels.All;
      GenericListener listener = new GenericListener();
      MySqlTrace.Listeners.Add(listener);

      StringBuilder sql = new StringBuilder("SELECT '");
      for (int i = 0; i < 400; i++)
        sql.Append("a");
      sql.Append("'");
      
      using (MySqlConnection logConnection = new MySqlConnection(this.conn))
      {
        logConnection.Open();
        MySqlCommand cmd = new MySqlCommand(sql.ToString(), logConnection);
        cmd.ExecuteNonQuery();
      }      

      Assert.Equal(24, listener.Strings.Count);
      Assert.True(listener.Strings[listener.Strings.Count - 5].EndsWith("SELECT ?", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Bug #57641	Substring out of range exception in ConsumeQuotedToken
    /// </summary>
    [Fact]
    public void QuotedTokenAt300()
    {
      MySqlTrace.Listeners.Clear();
      MySqlTrace.Switch.Level = SourceLevels.All;
      GenericListener listener = new GenericListener();
      MySqlTrace.Listeners.Add(listener);

      string sql = @"SELECT 1 AS `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA1`,  2 AS `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA2`,
                3 AS `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA3`,  4 AS `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA4`,
                5 AS `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA5`,  6 AS `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA6`;";

      using (MySqlConnection logConnection = new MySqlConnection(this.conn))
      {
        logConnection.Open();
        MySqlCommand cmd = new MySqlCommand(sql, logConnection);
        using (MySqlDataReader reader = cmd.ExecuteReader())
        {
        }
      }
    }

  }
}

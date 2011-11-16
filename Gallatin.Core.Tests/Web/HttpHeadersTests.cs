using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gallatin.Core.Web;
using NUnit.Framework;

namespace Gallatin.Core.Tests.Web
{
    [TestFixture]
    public class HttpHeadersTests
    {
        [Test]
        public void IndexTest()
        {
            List<KeyValuePair<string,string> > pairs = new List<KeyValuePair<string, string>>();
            pairs.Add(new KeyValuePair<string, string>("foo","bar"));
            pairs.Add(new KeyValuePair<string, string>("cat", "dog"));

            HttpHeaders headers = new HttpHeaders( pairs );

            Assert.That(headers["CAT"], Is.EqualTo("dog"));
            Assert.That(headers["bar"], Is.Null);
        }

        [Test]
        public void RemoveTest()
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            pairs.Add(new KeyValuePair<string, string>("foo", "bar"));
            pairs.Add(new KeyValuePair<string, string>("cat", "dog"));

            HttpHeaders headers = new HttpHeaders(pairs);

            Assert.That(headers["cat"], Is.EqualTo("dog"));

            headers.Remove( "nothing" );
            headers.Remove("CAT");

            Assert.That(headers["CAT"], Is.Null);
        }

        [Test]
        public void RenameKeyTest()
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            pairs.Add(new KeyValuePair<string, string>("foo", "bar"));
            pairs.Add(new KeyValuePair<string, string>("cat", "dog"));

            HttpHeaders headers = new HttpHeaders(pairs);

            headers.RenameKey("cat", "kitty");

            Assert.That(headers.AsEnumerable().First().Key, Is.EqualTo("foo"));
            Assert.That(headers.AsEnumerable().ElementAt(1).Key, Is.EqualTo("kitty"));
            Assert.That(headers["cat"], Is.Null);
        }

        [Test]
        public void RemoveKeyValueTest()
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            pairs.Add(new KeyValuePair<string, string>("content-encoding", "gzip;bark"));
            pairs.Add(new KeyValuePair<string, string>("count", "one;two;three"));
            pairs.Add(new KeyValuePair<string, string>("content-type", "text/html;charset=utf-8"));
            pairs.Add(new KeyValuePair<string, string>("foo", "bar"));

            HttpHeaders headers = new HttpHeaders(pairs);

            headers.RemoveKeyValue("content-encoding", "bark");
            headers.RemoveKeyValue("count", "two");
            headers.RemoveKeyValue("content-type", "text/html");
            headers.RemoveKeyValue("foo", "bar");

            Assert.That(headers.Count, Is.EqualTo(3));

            Assert.That(headers["content-encoding"], Is.EqualTo("gzip"));
            Assert.That(headers["content-type"], Is.EqualTo("charset=utf-8"));
            Assert.That(headers["count"], Is.EqualTo("one;three"));
        }

        [Test]
        public void UpsertTest()
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            pairs.Add(new KeyValuePair<string, string>("one", "1"));
            pairs.Add(new KeyValuePair<string, string>("two", "2"));
            pairs.Add(new KeyValuePair<string, string>("three", "3"));
            pairs.Add(new KeyValuePair<string, string>("two", "2"));

            HttpHeaders headers = new HttpHeaders(pairs);

            Assert.That(headers.Count, Is.EqualTo(4));
            
            headers.UpsertKeyValue("two", "4");

            Assert.That(headers.Count, Is.EqualTo(3), "The duplicate key matching the upsert key should have been removed");

            Assert.That(headers.AsEnumerable().ElementAt(0).Key, Is.EqualTo("one"));
            Assert.That(headers.AsEnumerable().ElementAt(1).Key, Is.EqualTo("two"));
            Assert.That(headers.AsEnumerable().ElementAt(2).Key, Is.EqualTo("three"));
            Assert.That(headers["two"], Is.EqualTo("4"));
        }
    }
}

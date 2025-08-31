using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace TestDelaunayGenerator.DCELMesh
{
    public class SerializerDCEL
    {
        /// <summary>
        /// Сериализовать <see cref="RestrictedDCEL"/>
        /// </summary>
        /// <param name="dcel"></param>
        /// <param name="path"></param>
        public static void SerializeXML(RestrictedDCEL dcel, string path)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
            };

            var serializer = new DataContractSerializer(typeof(RestrictedDCEL));
            //using (var writer = new FileStream(path, FileMode.OpenOrCreate))
            using (var writer = XmlWriter.Create(path, settings))
            {
                serializer.WriteObject(writer, dcel);
            }
        }


        /// <summary>
        /// Десериализовать <see cref="RestrictedDCEL"/>
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="FormatException"></exception>
        public static RestrictedDCEL DeserializeXML(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Файл по пути не найден! Путь: {path}");

            var settings = new XmlReaderSettings
            {
                
                CloseInput = true,
            };

            var serializer = new DataContractSerializer(typeof(RestrictedDCEL));
            using (var writer = XmlReader.Create(path, settings))
            {

                object objDcel = serializer.ReadObject(writer);
                if (!(objDcel is RestrictedDCEL))
                    throw new FormatException($"импортированная DCEL не является типом {nameof(RestrictedDCEL)}!");
                return (RestrictedDCEL)objDcel;
            }
        }
    }
}

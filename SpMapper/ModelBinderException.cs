using System;

namespace Kama.DatabaseModel
{
    public class ModelBinderException : Exception
    {
        public ModelBinderException(Exception e, string modelPropertyName, string fieldName)
        {
            Exception = e;
            ModelPropertyName = ModelPropertyName;
            FieldName = fieldName;
        }

        public Exception Exception { get; private set; }

        public string ModelPropertyName { get; private set; }

        public string FieldName { get; private set; }
    }
}

namespace TrafficDataCollection.Api.Models
{
    public class IOTHubResponse<T> where T : class
    {
        public int Code { get; set; }
        public string Msg { get; set; }
        public T Data { get; set; }

        // Factory methods for standard responses
        public static IOTHubResponse<T> Success(T data, string message = "success")
        {
            return new IOTHubResponse<T>
            {
                Code = 0, // Success
                Msg = message,
                Data = data
            };
        }

        public static IOTHubResponse<T> Error(int code, string message)
        {
            return new IOTHubResponse<T>
            {
                Code = code,
                Msg = message,
                Data = null
            };
        }

        public static IOTHubResponse<T> InternalError(string message = "Internal server error")
        {
            return new IOTHubResponse<T>
            {
                Code = 500,
                Msg = message,
                Data = null
            };
        }
    }

    public class IOTHubSearchResponse<T> where T : class
    {
        public IList<T> Data { get; set; }
        public long TotalRecords { get; set; }

        // Factory method for creating search responses
        public static IOTHubSearchResponse<T> Create(IList<T> data, long totalRecords)
        {
            return new IOTHubSearchResponse<T>
            {
                Data = data,
                TotalRecords = totalRecords
            };
        }
    }
}

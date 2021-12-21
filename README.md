
### AR.P2
The project consits of the following services
- The Manager service
- The Worker service

The Manager service accepts a binary stream of floating point numbers (or a file format that can be reduced to a FP number stream), through REST or gRPC.

Each incoming stream is given metadata.

It subdivides the stream into workable loads, each containing its own metadata, information about which is stored in a RabbitMQ message queue.

Worker services then process the messages from the message queue.
As part of the processing, the Worker services will obtain a part of the binary stream, perform their processing and store the result, while writing to another message queue.

When the worker service receives the last 

https://www.csvplot.com/
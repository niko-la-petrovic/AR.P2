
### AR.P2
The project consits of the following services
- The Manager service
- The Worker service

The Manager service accepts a binary stream of floating point numbers (or a file format that can be reduced to a FP number stream), through REST or gRPC.

Each incoming stream is given metadata.

It subdivides the stream into workable loads, each containing its own metadata.

TODO: 
- Complete the documentation.

Notes:
- Currently, there are some unnecessary files. The Worker service is also not used currently, but eventually I would like to use RabbitMQ to set up message-queue-based processing.

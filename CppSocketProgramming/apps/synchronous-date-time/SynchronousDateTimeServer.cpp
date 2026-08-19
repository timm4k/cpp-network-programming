#include "ApplicationSupport.h"

#include <exception>
#include <iostream>
#include <string>

int main(int argc, char* argv[])
{
    try
    {
        application::configure_console();
        const network::WinsockRuntime winsock;
        const auto port = network::parse_server_port(argc, argv, application::DateTimePort);
        const network::Socket listener = network::create_listener(port);
        std::cout << "Synchronous date-time server is listening on 127.0.0.1:" << port << '\n';

        while (true)
        {
            const network::Socket client = network::accept_sync(listener);
            const std::string request = network::receive_line(client);
            application::print_received(network::remote_endpoint(client), request);
            if (request == application::ExitCommand)
            {
                break;
            }
            network::send_line(client, application::make_date_time_response(request));
        }
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "Error: " << error.what() << '\n';
        return 1;
    }
}